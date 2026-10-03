using Microsoft.Xna.Framework;
using Point = Microsoft.Xna.Framework.Point;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.Shared.Content;
using ProjectZ.Shared.Drawing;
using ProjectZ.WpfCompatibility;
using Controls = ProjectZ.WpfCompatibility.Controls;
using Application = ProjectZ.WpfCompatibility.Application;
class Entry { [STAThread] static void Main() { using var game = new Probe(); game.Run(); } }
class TestScene(SceneManager manager) : Scene(manager);
class Probe : Game {
 readonly GraphicsDeviceManager graphics;
 SceneManager manager; TestScene scene; Application app;
 public Probe() { graphics = new(this) { PreferredBackBufferWidth=500, PreferredBackBufferHeight=250 }; Content.RootDirectory="Content"; }
 protected override void LoadContent() {
  var resources=new ContentContainer(Content,GraphicsDevice); resources.LoadAllContent();
  Services.AddService(typeof(SpriteBatch),new SpriteBatch(GraphicsDevice)); Services.AddService(typeof(GraphicsDevice),GraphicsDevice); Services.AddService(typeof(ContentContainer),resources);
  manager=new(this) { UseExternalInput=true, AutoResizeViewport=false, ExternalViewportSize=new Point(500,250) };
  scene=new(manager) { EnableDesignTimeControls=false }; manager.AddScene("probe",scene);manager.ActiveScene=scene; app=new(scene);
 }
 protected override void Draw(GameTime time) {
  try {
   foreach(var view in new Window[] {new LinkedCSharp.View(),new LinkedVisualBasic.View()}) {
    view.Show();manager.Tick(time);scene.Draw(time);
    var button=(Controls.Button)view.FindName("Action"); button.NativeElement.ValidationCheck(); var p=button.NativeElement.Position;
    manager.InjectMouseLeftDown((int)p.X+10,(int)p.Y+10,500,250); manager.InjectMouseLeftUp((int)p.X+10,(int)p.Y+10,500,250);
    int calls=(int)view.GetType().GetProperty("Calls").GetValue(view); string label=(string)view.GetType().GetProperty("DesignerLabel").GetValue(view);
    if(calls!=1 || ((Controls.TextBlock)view.FindName("Status")).Text!=label+"1")throw new Exception("Generated view event failed: "+view.GetType());
    Console.WriteLine("PASS compile-time designer and original code-behind: "+view.GetType()); view.Close();
   }
  } catch(Exception e) { Console.Error.WriteLine(e);Environment.ExitCode=1; }
  finally { app.Dispose();Exit(); }
 }
}
