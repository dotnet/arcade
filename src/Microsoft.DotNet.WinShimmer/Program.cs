using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;


namespace Microsoft.DotNet.WinShimmer
{
    class Program
    {
        /// <summary>
        /// The WinShimmer creates exe shims for windows tools
        /// </summary>
        /// <param name="args[0]">The name of the shim to be created</param>
        /// <param name="args[1]">The path to the executable to be shimmed</param>
        /// <param name="args[2]">The path to the directory where the shim is going to be output.</param>
        static void Main(string[] args)
        {
            if (args.Length != 3)
            {
                throw new ArgumentOutOfRangeException("args", $"WinShimmer was provided {args.Length} arguments instead of 3");
            }
            string shimName = args[0];
            string exePath = args[1];
            string outputDirectory = args[2];

            if (!File.Exists(exePath))
            {
                throw new FileNotFoundException($"The executable {exePath} was not found.");
            }
            if (!Directory.Exists(outputDirectory))
            {
                throw new DirectoryNotFoundException($"The specified output directory \"{outputDirectory}\" does not exist");
            }

            string outputLocation = $@"{outputDirectory}\{shimName}.exe";

            string compileReadyShimPath = $@"""{exePath.Replace(@"\", @"\\")}""";

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

                if (!emit.Success)
                {
                    throw new InvalidProgramException($"The generated program contained errors: \n{string.Join('\n', emit.Diagnostics.AsEnumerable())}");
                }
            }
        }
    }
}
