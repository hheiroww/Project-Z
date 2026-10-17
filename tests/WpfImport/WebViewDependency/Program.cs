using Microsoft.Web.WebView2.Core;
var host = typeof(ProjectZ.Shared.Html5.Html5EngineHost);
if (host.GetMethod("ExecuteScriptAsync") == null) throw new Exception("Native browser host contract missing");
var environment = new CoreWebView2EnvironmentOptions();
if (string.IsNullOrWhiteSpace(CoreWebView2Environment.GetAvailableBrowserVersionString())) throw new Exception("Browser runtime missing");
Console.WriteLine("WEBVIEW_DEPENDENCY_OK");
