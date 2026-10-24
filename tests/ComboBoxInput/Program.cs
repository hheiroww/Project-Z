using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ProjectZ.Shared.Content;
using ProjectZ.Shared.Drawing;
using ProjectZ.Shared.Drawing.UI.Input;
using ProjectZ.Shared.Drawing.UI.Primitives;
using Color = Microsoft.Xna.Framework.Color;
using Point = Microsoft.Xna.Framework.Point;
using Keys = Microsoft.Xna.Framework.Input.Keys;
using ComboBox = ProjectZ.Shared.Drawing.UI.Input.ComboBox;
using Button = ProjectZ.Shared.Drawing.UI.Input.Button;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

static class Program {
    [STAThread] static int Main(string[] args) { if(args[0]=="--gpu-pattern"){using var pattern=new GpuPattern(args[1]); pattern.Run();return 0;} using var game=new Probe(args[0]); game.Run(); return game.Passed?0:1; }
}
sealed class InputScene(SceneManager manager):Scene(manager);
sealed class Probe:Game {
    readonly string output; readonly GraphicsDeviceManager graphics; readonly List<string> checks=new();
    SceneManager manager; InputScene scene; ComboBox combo; ScrollViewer scroll; int frame,defaultClicks; SpriteBatch batch; ContentContainer resources;
    public bool Passed; bool highlightPixels;
    public Probe(string path) { IsFixedTimeStep=false; output=Path.GetFullPath(path); Directory.CreateDirectory(output); graphics=new(this){PreferredBackBufferWidth=720,PreferredBackBufferHeight=480,GraphicsProfile=GraphicsProfile.HiDef}; Window.Title="Project-Z dropdown input acceptance"; }
    protected override void LoadContent() {
        batch=new(GraphicsDevice); resources=new(Content,GraphicsDevice);resources.LoadAllContent();
        Services.AddService(typeof(SpriteBatch),batch);Services.AddService(typeof(GraphicsDevice),GraphicsDevice);Services.AddService(typeof(ContentContainer),resources);
        manager=new(this){UseExternalInput=true,AutoResizeViewport=false,ExternalViewportSize=new Point(720,480)};
        scene=new(manager){EnableDesignTimeControls=false,BackgroundColor=new Color(8,18,24)}; manager.AddScene("input",scene); manager.ActiveScene=scene;
        scroll=new(scene){Position=new(20,80),Size=new(640,280),CanScrollVertically=true,Padding=new(0)};
        var body=new RectangleElement(scene){Size=new(610,700),Padding=new(0),BackgroundColor=Color.Transparent};
        combo=new(scene){Margin=new(300,480,0,0),Size=new(270,30),Items=new(){"Custom / current","Matrix","Ocean","Amethyst","Amber"},SelectedIndex=0};
        body.Children.Add(combo); scroll.Content=body; scene.AddElement(scroll); scroll.ScrollTo(new(0,420));
        scene.AddElement(new Button(scene){Position=new(320,200),Size=new(270,32),Text="Underlying button"});
        var accept=new Button(scene){Position=new(20,410),Size=new(200,32),Text="Default action",IsDefault=true};accept.MouseLeftClick+=_=>defaultClicks++;scene.AddElement(accept);
        GraphicsQuality.ForDevice(GraphicsDevice).AntiAliasingSamples=16;
    }
    void Check(bool good,string name) { if(!good)throw new Exception(name); checks.Add("PASS "+name); }
    void Click(int x,int y) {manager.InjectMouseMove(x,y,720,480);manager.InjectMouseLeftDown(x,y,720,480);manager.InjectMouseLeftUp(x,y,720,480);}
    void Tap(Keys key){manager.InjectKeyDown(key);manager.InjectKeyUp(key);}
    protected override void Update(GameTime time) {
        try {manager.Tick(time); frame++;
            if(frame==8){File.WriteAllText(Path.Combine(output,"geometry.txt"),$"combo={combo.Position}; rect={combo.Rectangle}; scroll={scroll.ScrollOffset}");Click(combo.Rectangle.Center.X,combo.Rectangle.Center.Y);}
            if(frame==12){Check(combo.IsDropDownOpen,"scrolled header opens its dropdown");var p=new Point(combo.Rectangle.Center.X,combo.Rectangle.Bottom+28+14);manager.InjectMouseMove(p.X,p.Y,720,480);manager.InjectMouseLeftDown(p.X,p.Y,720,480);}
            if(frame==14){Check(combo.IsDropDownOpen,"pressed row retains its keyboard owner across frames");var p=new Point(combo.Rectangle.Center.X,combo.Rectangle.Bottom+28+14);manager.InjectMouseLeftUp(p.X,p.Y,720,480);Check(combo.SelectedIndex==1&&!combo.IsDropDownOpen,"separate mouse-down/up frames select a popup row above underlying controls");}
            if(frame==16){scene.FocusElement(combo);Tap(Keys.Down);Check(ReferenceEquals(scene.FocusedElement,combo)&&combo.IsDropDownOpen&&combo.HighlightedIndex==2&&combo.SelectedIndex==1,"Down opens a visible preview without committing");}
            if(frame==20){Check(highlightPixels&&combo.DropDownElements[2].BorderColor==combo.DropDownIndicatorColor,"GPU pixels show a distinct soft keyboard highlight and slim indicator");Tap(Keys.Escape);Check(combo.SelectedIndex==1&&!combo.IsDropDownOpen,"Escape cancels preview");Tap(Keys.Down);Tap(Keys.Enter);Check(combo.SelectedIndex==2&&!combo.IsDropDownOpen&&defaultClicks==0,"Enter commits dropdown without activating the dialog default");}
            if(frame==24){combo.ItemIsEnabled=i=>i!=3;Tap(Keys.Down);Check(combo.HighlightedIndex==4,"arrow navigation skips disabled items");Tap(Keys.Home);Check(combo.HighlightedIndex==0,"Home selects the first preview");Tap(Keys.End);Check(combo.HighlightedIndex==4,"End selects the last enabled preview");Tap(Keys.Enter);Tap(Keys.Space);Check(combo.IsDropDownOpen,"Space reopens the focused dropdown");Tap(Keys.Tab);Check(!combo.IsDropDownOpen&&!ReferenceEquals(scene.FocusedElement,combo),"Tab closes the list and advances focus");Passed=true;File.WriteAllLines(Path.Combine(output,"result.txt"),checks);Exit();}
        }catch(Exception error){File.WriteAllLines(Path.Combine(output,"result.txt"),checks.Append("FAIL "+error));Exit();}
        base.Update(time);
    }
    protected override void Draw(GameTime time){manager.Draw(time);if(frame==19){var pixels=new Color[720*480];GraphicsDevice.GetBackBufferData(pixels);var selected=combo.DropDownElements[2].Rectangle;var other=combo.DropDownElements[1].Rectangle;var a=pixels[selected.Center.Y*720+selected.Right-12];var b=pixels[other.Center.Y*720+other.Right-12];highlightPixels=a.G>b.G+15;using var texture=new Texture2D(GraphicsDevice,720,480);texture.SetData(pixels);using var file=File.Create(Path.Combine(output,"dropdown-keyboard.png"));texture.SaveAsPng(file,720,480);}base.Draw(time);}
}
sealed class GpuPattern:Game {
    readonly string ready; readonly GraphicsDeviceManager graphics; SpriteBatch batch; Texture2D pixel; bool announced; readonly long started=Environment.TickCount64;
    public GpuPattern(string path){ready=path;graphics=new(this){PreferredBackBufferWidth=1100,PreferredBackBufferHeight=900};}
    protected override void Initialize(){Window.Title="Project-Z hardware backdrop fixture";Window.Position=new Point(20,20);base.Initialize();}
    protected override void LoadContent(){batch=new(GraphicsDevice);pixel=new(GraphicsDevice,1,1);pixel.SetData(new[]{Color.White});}
    protected override void Draw(GameTime time){GraphicsDevice.Clear(Color.Cyan);batch.Begin();for(int y=0;y<900;y+=128)for(int x=0;x<1100;x+=128)if((x/128+y/128)%2==0)batch.Draw(pixel,new Rectangle(x,y,128,128),Color.Red);batch.End();if(!announced){announced=true;File.WriteAllText(ready,ProjectZ.Windows.Composition.WindowsComposition.ResolveHwnd(this).ToInt64().ToString());}if(Environment.TickCount64-started>45000)Exit();base.Draw(time);}
}
