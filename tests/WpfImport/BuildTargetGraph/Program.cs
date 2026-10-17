using System.Security.Cryptography;
var directory = AppContext.BaseDirectory;
if (!File.Exists(Path.Combine(directory, "Tools", "BuildTargetWorker.dll"))) throw new Exception("Missing worker dependency");
var expected = File.ReadAllText(Path.Combine(directory, "release.sha256")).Trim();
var actual = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(directory, "BuildTargetGraph.dll"))));
if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase)) throw new Exception("Release hash does not match");
if (typeof(Program).Assembly.GetName().Version!.Major != 3) throw new Exception("Assembly version lost");
Console.WriteLine("BUILD_TARGET_GRAPH_OK");
