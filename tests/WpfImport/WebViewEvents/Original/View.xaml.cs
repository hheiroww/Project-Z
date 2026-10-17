using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
namespace BrowserEvents;
public partial class View : UserControl
{
    public readonly List<string> Events = new();
    public bool BlockPointer, BlockText;
    public int NavigationCount, RowClickCount, TextChangeCount;
    public object? LastSource;
    public bool MotionCompleted;
    public TranslateTransform Motion = new();
    public void StartMotion()
    {
        Action.RenderTransform=Motion;
        var animation=new DoubleAnimation(0,16,TimeSpan.FromMilliseconds(80));
        var storyboard=new Storyboard();Storyboard.SetTarget(animation,Motion);Storyboard.SetTargetProperty(animation,new PropertyPath(TranslateTransform.XProperty));
        storyboard.Children.Add(animation);storyboard.Completed+=(_,_)=>MotionCompleted=true;storyboard.Begin();
    }
    public View()
    {
        InitializeComponent();
        System.Windows.Automation.AutomationProperties.SetName(this, "Native static-call regression");
        Browser.CreationProperties = new CoreWebView2CreationProperties { UserDataFolder = Path.Combine(Path.GetTempPath(), "ProjectZ-WebViewEvents-" + Environment.ProcessId) };
        Rows.ItemsSource = new[] { "one", "two" };
    }
    void RootPreview(object sender, MouseButtonEventArgs e) { Events.Add("preview-root"); if (BlockPointer) e.Handled = true; }
    void RootDown(object sender, MouseButtonEventArgs e) => Events.Add("bubble-root");
    void ActionDown(object sender, MouseButtonEventArgs e) => Events.Add("bubble-action");
    void ActionClicked(object sender, RoutedEventArgs e) { Events.Add("click"); LastSource = e.OriginalSource; }
    void RightPreview(object sender, MouseButtonEventArgs e) { Events.Add("right-preview"); e.Handled = true; }
    void RowClicked(object sender, RoutedEventArgs e) { RowClickCount++; LastSource = sender; }
    void RowClicked() => throw new InvalidOperationException("The unrelated overload must not be bound.");
    void RootKey(object sender, KeyEventArgs e) { Events.Add("preview-key"); if (e.Key == Key.F5) e.Handled = true; }
    void EditorPreviewText(object sender, TextCompositionEventArgs e) { Events.Add("preview-text"); if (BlockText) e.Handled = true; }
    void EditorText(object sender, TextCompositionEventArgs e) => Events.Add("text");
    void RootTextChanged(object sender, TextChangedEventArgs e) => TextChangeCount++;
    void BrowserNavigated(object? sender, CoreWebView2NavigationCompletedEventArgs e) { if (e.IsSuccess) NavigationCount++; LastSource = sender; }
}
