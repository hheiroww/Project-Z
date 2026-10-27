using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ProjectZ.Shared.Content;
using ProjectZ.Shared.Drawing;
using ProjectZ.Shared.Drawing.UI;
using ProjectZ.Shared.Drawing.UI.Input;
using ProjectZ.Shared.Drawing.UI.Primitives;
using ComboBox = ProjectZ.Shared.Drawing.UI.Input.ComboBox;
using Color = Microsoft.Xna.Framework.Color;
using Point = Microsoft.Xna.Framework.Point;
using Rectangle = Microsoft.Xna.Framework.Rectangle;
using Keys = Microsoft.Xna.Framework.Input.Keys;

static class Program {
    [STAThread] static int Main(string[] args) { using var game = new Probe(args[0]); game.Run(); return game.Passed ? 0 : 1; }
}
sealed class TestScene(SceneManager manager) : Scene(manager);
sealed class Probe : Game {
    readonly string output; readonly GraphicsDeviceManager graphics; readonly List<string> checks = [];
    SceneManager manager; TestScene scene; ComboBox combo; ContextMenu menu; int frame; bool blurred, sharp, disabled; RectangleElement thumb;
    public bool Passed;
    public Probe(string path) { output=Path.GetFullPath(path); Directory.CreateDirectory(output); graphics=new(this){PreferredBackBufferWidth=720,PreferredBackBufferHeight=480,GraphicsProfile=GraphicsProfile.HiDef}; IsFixedTimeStep=false; Window.Title="Project-Z floating controls acceptance"; }
    protected override void LoadContent() {
        var batch=new SpriteBatch(GraphicsDevice); var resources=new ContentContainer(Content,GraphicsDevice); resources.LoadAllContent();
        Services.AddService(typeof(SpriteBatch),batch); Services.AddService(typeof(GraphicsDevice),GraphicsDevice); Services.AddService(typeof(ContentContainer),resources);
        manager=new(this){UseExternalInput=true,AutoResizeViewport=false,ExternalViewportSize=new Point(720,480)};
        scene=new(manager){EnableDesignTimeControls=false,BackgroundColor=new Color(12,18,22)}; manager.AddScene("test",scene); manager.ActiveScene=scene;
        for(int x=380;x<700;x++) scene.AddElement(new RectangleElement(scene){Position=new(x,40),Size=new(1,400),BackgroundColor=x%2==0?Color.White:Color.Black});
        menu=new(scene){Position=new(400,100),Size=new(240,180),Padding=new(0),BackgroundColor=new Color(0,0,0,24),BackgroundBlur=12,CornerRadius=16};
        menu.Children.Add(new TextElement(scene){Text="Sharp foreground text",ForegroundColor=Color.White,Size=new(210,24)});
        menu.Children.Add(new RectangleElement(scene){Size=new(40,20),BackgroundColor=Color.White}); scene.AddElement(menu); menu.IsOpen=true;
        combo=new(scene){Position=new(20,45),Size=new(270,30),Items=Enumerable.Range(0,120).Select(i=>$"Instrument {i:000}").ToList(),SelectedIndex=0,BackgroundBlur=12,MaxDropDownHeight=220}; scene.AddElement(combo);
        GraphicsQuality.ForDevice(GraphicsDevice).AntiAliasingSamples=16;
    }
    void Check(bool good,string name) { if(!good)throw new Exception(name); checks.Add("PASS "+name); }
    void Move(int x,int y)=>manager.InjectMouseMove(x,y,720,480);
    void Click(int x,int y){Move(x,y);manager.InjectMouseLeftDown(x,y,720,480);manager.InjectMouseLeftUp(x,y,720,480);}
    void Tap(Keys key){manager.InjectKeyDown(key);manager.InjectKeyUp(key);}
    protected override void Update(GameTime time) {
        try {
            manager.Tick(time); frame++;
            if(frame==8) { Check(blurred&&sharp,"GPU backdrop is blurred while foreground remains sharp"); GraphicsQuality.ForDevice(GraphicsDevice).EffectsEnabled=false; scene.RequestRender(); }
            if(frame==12) {Check(disabled,"Disabling effects restores the unblurred backdrop");GraphicsQuality.ForDevice(GraphicsDevice).EffectsEnabled=true; Click(60,60);}
            if(frame==16) {var popup=combo.DropDownScrollViewer;Check(combo.IsDropDownOpen&&popup.VerticalScrollBarIsVisible&&popup.Size.Y<=220&&popup.Rectangle.Bottom<=480,"120 items share a bounded popup with a visible scrollbar");manager.InjectMouseWheel(popup.Rectangle.Center.X,popup.Rectangle.Center.Y,720,480,-120);}
            if(frame==22) {Check(combo.DropDownScrollViewer.ScrollOffset.Y>0,"Mouse wheel scrolls the popup");combo.DropDownScrollViewer.StopScrolling();scene.FocusElement(combo);Tap(Keys.End);}
            if(frame==26) {var popup=combo.DropDownScrollViewer;var last=combo.DropDownElements[119].Rectangle;Check(combo.HighlightedIndex==119&&last.Top>=popup.Rectangle.Top&&last.Bottom<=popup.Rectangle.Bottom,"End reveals the final instrument");Tap(Keys.Enter);Check(combo.SelectedIndex==119&&!combo.IsDropDownOpen,"The final instrument can be selected");Tap(Keys.Space);}
            if(frame==30) {Check(combo.DropDownScrollViewer.ScrollOffset.Y>0,"Reopening reveals the selected instrument");Tap(Keys.Home);}
            if(frame==34) {Check(combo.HighlightedIndex==0&&combo.DropDownScrollViewer.ScrollOffset.Y==0,"Home returns to the first instrument");var popup=combo.DropDownScrollViewer;thumb=popup.Children.OfType<RectangleElement>().Single(x=>x.zIndex==int.MaxValue&&x.isVisible&&x.Size.X==12);Move(thumb.Rectangle.Center.X,thumb.Rectangle.Center.Y);manager.InjectMouseLeftDown(thumb.Rectangle.Center.X,thumb.Rectangle.Center.Y,720,480);}
            if(frame==36) Move(thumb.Rectangle.Center.X,combo.DropDownScrollViewer.Rectangle.Bottom-2);
            if(frame==38) {var popup=combo.DropDownScrollViewer;manager.InjectMouseLeftUp(popup.Rectangle.Right-6,popup.Rectangle.Bottom-2,720,480);Check(popup.ScrollOffset.Y>popup.ScrollableSize.Y*.9,"Dragging the scrollbar reaches the bottom");Tap(Keys.Escape);combo.Position=new(20,440);Click(60,455);}
            if(frame==42) {Check(combo.DropDownScrollViewer.Rectangle.Bottom<=combo.Rectangle.Top,"A low header opens upward inside the viewport");combo.IsDropDownOpen=false;menu.IsOpen=true;}
            if(frame==46) {Check(SceneElement.RenderExceptionCount==0,"Floating controls render without GPU exceptions");Passed=true;File.WriteAllLines(Path.Combine(output,"result.txt"),checks);Exit();}
        }catch(Exception error){File.WriteAllLines(Path.Combine(output,"result.txt"),checks.Append("FAIL "+error));Exit();}
        base.Update(time);
    }
    protected override void Draw(GameTime time) {
        manager.Draw(time);
        if(frame==6||frame==10||frame==44){var pixels=new Color[720*480];GraphicsDevice.GetBackBufferData(pixels);double contrast=Enumerable.Range(430,150).Average(x=>Math.Abs((int)pixels[150*720+x].R-pixels[150*720+x+1].R));
            if(frame==6){blurred=contrast<30&&pixels[150*720+500].R is >40 and <210;var bar=menu.Children.OfType<RectangleElement>().Single();sharp=pixels[bar.Rectangle.Center.Y*720+bar.Rectangle.Center.X].R>245;}
            if(frame==10)disabled=contrast>180;
            using var texture=new Texture2D(GraphicsDevice,720,480);texture.SetData(pixels);using var file=File.Create(Path.Combine(output,$"floating-{frame}.png"));texture.SaveAsPng(file,720,480);
        }
        base.Draw(time);
    }
}
