using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SignCheck.Logging;
using Microsoft.SignCheck.Interop.PortableExecutable;
using Microsoft.Tools.WindowsInstallerXml;
using System.IO;

namespace Microsoft.SignCheck.Verification
{
    public class ExeVerifier : PortableExecutableVerifier
    {
        public ExeVerifier(Log log, Exclusions exclusions, SignatureVerificationOptions options, string fileExtension) :
            base(log, exclusions, options, fileExtension)
        {

        }

        public override SignatureVerificationResult VerifySignature(string path, string parent)
        {
            // Let the base class take care of verifying the AuthentiCode/StrongName
            SignatureVerificationResult svr = base.VerifySignature(path, parent);

            if (VerifyRecursive)
            {
                if (PEHeader.ImageSectionHeaders.Select(s => s.SectionName).Contains(".wixburn"))
                {
                    Log.WriteMessage(LogVerbosity.Diagnostic, SignCheckResources.DiagSectionHeader, ".wixburn");
                    Log.WriteMessage(LogVerbosity.Detailed, SignCheckResources.WixBundle, svr.FullPath);
                    Unbinder unbinder = null;

                    try
                    {
                        Log.WriteMessage(LogVerbosity.Diagnostic, SignCheckResources.DiagExtractingFileContents, svr.TempPath);
                        unbinder = new Unbinder();
                        unbinder.Message += UnbinderEventHandler;
                        Output o = unbinder.Unbind(svr.FullPath, OutputType.Bundle, svr.TempPath);

                        if (Directory.Exists(svr.TempPath))
                        {
                            foreach (string file in Directory.EnumerateFiles(svr.TempPath, "*.*", SearchOption.AllDirectories))
                            {
                                svr.NestedResults.Add(VerifyFile(Path.GetFullPath(file), svr.Filename));
                            }
                        }

                        Directory.Delete(svr.TempPath, recursive: true);
                    }
                    finally
                    {
                        unbinder.DeleteTempFiles();
                    }
                }
            }

            // TODO: Check for SFXCAB, IronMan, etc.

            return svr;
        }

        /// <summary>
        /// Event handler for WiX Burn to extract a bundle.
        /// </summary>
        private void UnbinderEventHandler(object sender, MessageEventArgs e)
        {
            Log.WriteMessage(LogVerbosity.Detailed, String.Format("{0}|{1}|{2}|{3}", e.Id, e.Level, e.ResourceName, e.SourceLineNumbers));
        }
    }
}
