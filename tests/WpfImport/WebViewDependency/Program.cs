using Microsoft.Web.WebView2.Core;
var host = typeof(ProjectZ.WpfCompatibility.Controls.WebView2);
if (host.GetMethod("ExecuteScriptAsync") == null || host.GetMethod("EnsureCoreWebView2Async") == null) throw new Exception("Native browser control contract missing");
if (typeof(ProjectZ.Shared.Drawing.Scene).Assembly.GetType("ProjectZ.Shared.Html5.Html5EngineHost") != null) throw new Exception("Removed HTML5 engine host is still present");
var environment = new CoreWebView2EnvironmentOptions();
if (string.IsNullOrWhiteSpace(CoreWebView2Environment.GetAvailableBrowserVersionString())) throw new Exception("Browser runtime missing");
Console.WriteLine("WEBVIEW_DEPENDENCY_OK");
