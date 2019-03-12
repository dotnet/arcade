using System;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.DotNet.GenPartialFacades
{
    class Program
    {
        // temp file to test the changes. This will be removed after through testing
        static void Main(string[] args)
        {
            string AssemblyName = "System.Runtime.WindowsRuntime";
            string srcdirectoryName = @"C:\git\corefx\src\" + AssemblyName + @"\src\";

            string Contracts = @"C:\git\corefx\artifacts\bin\ref/uap//" + AssemblyName + ".dll";
            string[] seedTypePreferencesUnsplit = new string[] { "Windows.Foundation.Point=System.Private.Interop", "Windows.Foundation.Size=System.Private.Interop", "Windows.Foundation.Rect=System.Private.Interop" };
            string[] typeAssemblyConversions = new string[] { "System.Private.Interop=Alias_System_Private_Interop" };
            string constants = @"DEBUG;TRACE;uapaot;TRACE";
            string compileFiles = GetListOfFiles(srcdirectoryName);
            //string seeds = @"C:\git\corefx\artifacts\bin\ref/netcoreapp/netstandard.dll,C:\git\corefx\artifacts\bin\ref/netcoreapp/System.Buffers.dll,C:\git\corefx\artifacts\bin\ref/netcoreapp/System.Collections.dll,C:\git\corefx\artifacts\bin\ref/netcoreapp/System.Diagnostics.Debug.dll,C:\git\corefx\artifacts\bin\ref/netcoreapp/System.Diagnostics.Tools.dll,C:\git\corefx\artifacts\bin\ref/netcoreapp/System.Linq.dll,C:\git\corefx\artifacts\bin\ref/netcoreapp/System.Memory.dll,C:\git\corefx\artifacts\bin\ref/netcoreapp/System.Resources.ResourceManager.dll,C:\git\corefx\artifacts\bin\ref/netcoreapp/System.Runtime.dll,C:\git\corefx\artifacts\bin\ref/netcoreapp/System.Runtime.Extensions.dll,C:\git\corefx\artifacts\bin\ref/netcoreapp/System.Runtime.InteropServices.dll,C:\git\corefx\artifacts\bin\ref/netcoreapp/System.Text.Encoding.Extensions.dll,C:\git\corefx\artifacts\bin\ref/netcoreapp/System.Threading.dll,C:\git\corefx\artifacts\bin\ref/netcoreapp/System.Threading.Overlapped.dll,C:\git\corefx\artifacts\bin\ref/netcoreapp/System.Threading.Tasks.dll";
            string seeds = @"C:\Users\anagniho\.nuget\packages\microsoft.targetingpack.private.netnative\1.1.0-beta-27421-00\lib\uap10.0\System.Private.CoreLib.dll,C:\Users\anagniho\.nuget\packages\microsoft.targetingpack.private.netnative\1.1.0-beta-27421-00\lib\uap10.0\System.Private.Interop.dll,C:\git\corefx\artifacts\bin\ref/uap/mscorlib.dll,C:\git\corefx\artifacts\bin\ref/uap/netstandard.dll,C:\git\corefx\artifacts\bin\ref/uap/Windows.winmd,C:\git\corefx\artifacts\bin\System.Runtime\uapaot-Windows_NT-Debug\System.Runtime.dll,C:\git\corefx\artifacts\bin\System.Runtime.Extensions\uapaot-Windows_NT-Debug\System.Runtime.Extensions.dll";
            GenPartialFacadesGenerator.Execute(
                seeds,
                Contracts,
                compileFiles,
                AssemblyName,
                constants,
                seedTypePreferencesUnsplit: seedTypePreferencesUnsplit,
                typeAssemblyConversions:  typeAssemblyConversions);
        }


        private static string GetListOfFiles(string DirectoryName)
        {
            var files = Directory.EnumerateFiles(DirectoryName, "*", SearchOption.AllDirectories);
            string result = "";
            foreach (var item in files)
            {
                result += ";" + item.ToString();

            }

            return result.Substring(1);
        }
    }
}
