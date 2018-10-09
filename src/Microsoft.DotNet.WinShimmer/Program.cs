using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;


namespace Microsoft.DotNet.WinShimmer
{
    class Program
    {
        static void Main(string[] args)
        {
            var shimName = args[0];
            var exePath = args[1];
            var outputDirectory = args[2];

            var outputLocation = $@"{outputDirectory}\{shimName}.exe";

            var compileReadyShimPath = $@"""{exePath.Replace(@"\", @"\\")}""";

            var compilation = CSharpCompilation.Create(shimName)
                .AddReferences(
                    // NOTE: yes, I hate hardcoding these dlls as much as everyone else does.
                    // However, the output exe needs framework dlls, and there doesn't seem to be an easy way to obtain the location
                    // of framework dlls from within a .NET Core project. These locations should be constant across most Win machines
                    // we use in our queues.
                    MetadataReference.CreateFromFile(@"C:\Windows\Microsoft.NET\Framework\v4.0.30319\mscorlib.dll"),
                    MetadataReference.CreateFromFile(@"C:\WINDOWS\Microsoft.Net\assembly\GAC_MSIL\System.Core\v4.0_4.0.0.0__b77a5c561934e089\System.Core.dll"),
                    MetadataReference.CreateFromFile(@"C:\WINDOWS\Microsoft.Net\assembly\GAC_MSIL\System\v4.0_4.0.0.0__b77a5c561934e089\System.dll")
                    )
                .AddSyntaxTrees(CSharpSyntaxTree.ParseText($@"
using System;
using System.Diagnostics;
using System.Linq;

class Program
{{
    public static int Main(string[] args)
    {{
        var arguments = string.Join("" "", args.Select(a => $""\""{{a}}\""""));
        var psi = new ProcessStartInfo
        {{
	        FileName = {compileReadyShimPath},
	        UseShellExecute = false,
	        Arguments = arguments,
			CreateNoWindow = false,
        }};
        var process = Process.Start(psi);
		Console.CancelKeyPress += (s, e) => {{ e.Cancel = true; }};
        process.WaitForExit();
        return process.ExitCode;
    }}
}}
"));
            using (var exe = new FileStream(outputLocation, FileMode.Create))
            using (var resources = compilation.CreateDefaultWin32Resources(true, true, null, null))
            {
                var emit = compilation.Emit(exe, win32Resources:resources);
            }
        }
    }
}
