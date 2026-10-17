var builder = WebApplication.CreateBuilder(args);
if (string.IsNullOrWhiteSpace(builder.Environment.ApplicationName)) throw new Exception("Web SDK host failed");
Console.WriteLine("SDK_WEB_OK");
