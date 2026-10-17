using System;
using System.IO;
using System.Reflection;

namespace Fixture
{
    class Program
    {
        static void Main()
        {
            var assembly = Assembly.GetExecutingAssembly();
            using (var stream = assembly.GetManifestResourceStream("Fixture.Payload"))
            {
                if (stream == null)
                {
                    throw new InvalidOperationException("RESOURCE_NOT_FOUND");
                }

                using (var reader = new StreamReader(stream))
                {
                    string content = reader.ReadToEnd();
                    if (content != "This is the embedded resource.")
                    {
                        throw new InvalidOperationException("RESOURCE_CONTENT_MISMATCH");
                    }
                }
            }

            Console.WriteLine("DEPLOYMENT_RESOURCE_OK");
        }
    }
}