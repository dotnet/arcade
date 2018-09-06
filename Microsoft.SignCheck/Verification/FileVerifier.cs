using System;
using System.IO;
using Microsoft.SignCheck.Logging;

namespace Microsoft.SignCheck.Verification
{
    public class FileVerifier
    {
        /// <summary>
        /// A set of files and file patterns to exclude from verification.
        /// </summary>
        public Exclusions Exclusions
        {
            get;
            private set;
        }

        /// <summary>
        /// The file extension associated with the verifier.
        /// </summary>
        public string FileExtension
        {
            get;
            private set;
        }

        /// <summary>
        /// The Log to use for writing output during verification.
        /// </summary>
        protected Log Log
        {
            get;
            private set;
        }

        /// <summary>
        ///
        /// </summary>
        public SignatureVerificationOptions Options
        {
            get;
            private set;
        }

        protected bool VerifyAuthenticodeTimestamps
        {
            get
            {
                return (Options & SignatureVerificationOptions.VerifyAuthentiCodeTimestamps) == SignatureVerificationOptions.VerifyAuthentiCodeTimestamps;
            }            
        }

        protected bool VerifyJarSignatures
        {
            get
            {
                return (Options & SignatureVerificationOptions.VerifyJarSignatures) == SignatureVerificationOptions.VerifyJarSignatures;
            }
        }

        protected bool VerifyRecursive
        {
            get
            {
                return (Options & SignatureVerificationOptions.VerifyRecursive) == SignatureVerificationOptions.VerifyRecursive;
            }
        }

        protected bool VerifyStrongNameSignature
        {
            get
            {
                return (Options & SignatureVerificationOptions.VerifyStrongNameSignature) == SignatureVerificationOptions.VerifyStrongNameSignature;
            }
        }

        protected bool VerifyXmlSignatures
        {
            get
            {
                return (Options & SignatureVerificationOptions.VerifyXmlSignatures) == SignatureVerificationOptions.VerifyXmlSignatures;
            }
        }

        public FileVerifier()
        {

        }

        /// <summary>
        /// Create a new FileVerifier instance.
        /// </summary>
        /// <param name="log">The Log to use for writing output during verification.</param>
        /// <param name="exclusions">The set of exclusions to check to determine if a file is excluded from verification.</param>
        /// <param name="options"></param>
        /// <param name="fileExtension">The file extension associated with the FileVerifier, e.g. ".zip" or ".dll".</param>
        public FileVerifier(Log log, Exclusions exclusions, SignatureVerificationOptions options, string fileExtension)
        {
            Log = log ?? throw new ArgumentNullException("log");
            Exclusions = exclusions ?? throw new ArgumentNullException("exclusions");
            Options = options;
            FileExtension = fileExtension;
        }

        /// <summary>
        /// Verifies the signature of a file.
        /// </summary>
        /// <param name="path">The path of the file to verify</param>
        /// <param name="parent">The parent file of the file to verify or null if this is a top-level file.</param>
        /// <returns>A SignatureVerificationResult containing detail about the verification result.</returns>
        public virtual SignatureVerificationResult VerifySignature(string path, string parent)
        {
            return SignatureVerificationResult.UnsupportedFileTypeResult(path, parent);
        }

        /// <summary>
        /// Verify the signature of a single file.
        /// </summary>
        /// <param name="path">The path of the file to verify.</param>
        /// <param name="parent">The name of parent container, e.g. an MSI or VSIX. Can be null when there is no parent container.</param>
        /// <returns>The verification result.</returns>
        public SignatureVerificationResult VerifyFile(string path, string parent)
        {
            Log.WriteMessage(LogVerbosity.Detailed, String.Format(SignCheckResources.ProcessingFile, Path.GetFileName(path), String.IsNullOrEmpty(parent) ? SignCheckResources.NA : parent));

            string extension = Path.GetExtension(path);
            FileVerifier fileVerifier = SignatureVerificationManager.GetFileVerifier(path);

            SignatureVerificationResult svr;
            svr = fileVerifier.VerifySignature(path, parent);

            Log.WriteMessage(LogVerbosity.Diagnostic, String.Format(SignCheckResources.DiagFirstExclusion, path));
            if (Exclusions.Count > 0)
            {
                svr.IsExcluded = Exclusions.IsParentExcluded(parent) || Exclusions.IsFileExcluded(path);
            }

            // Include the full path for top-level files
            if (String.IsNullOrEmpty(parent))
            {
                svr.AddDetail(DetailKeys.File, SignCheckResources.DetailFullName, svr.FullPath);
            }

            return svr;
        }

        /// <summary>
        /// Check whether the file specified file is excluded from verification and update the SignatureVerificationResult.
        /// An exclusion entry for the file based on its name, alias and parent is also generated.
        /// </summary>
        /// <param name="result">The result to update</param>
        /// <param name="alias">The alias of the file</param>
        /// <param name="fullName"></param>
        /// <param name="parent"></param>
        protected void CheckAndUpdateExclusion(SignatureVerificationResult result, string alias, string fullName, string parent)
        {
            if (Exclusions.Count > 0)
            {
                result.IsExcluded |= Exclusions.IsFileExcluded(fullName);
            }

            result.ExclusionEntry = String.Join(";", String.Join("|", alias, fullName), parent, String.Empty);
            Log.WriteMessage(LogVerbosity.Diagnostic, SignCheckResources.DiagGenerateExclusion, result.Filename, result.ExclusionEntry);
        }

        /// <summary>
        /// Create a directory using the specified path.
        /// </summary>
        /// <param name="path">The directory to create.</param>
        protected void CreateDirectory(string path)
        {
            Log.WriteMessage(LogVerbosity.Diagnostic, String.Format(SignCheckResources.DiagCreatingFolder, path));
            Directory.CreateDirectory(path);
        }

        /// <summary>
        /// Deletes the specified directory and all subdirectories.
        /// </summary>
        /// <param name="path">The directory to delete.</param>
        protected void DeleteDirectory(string path)
        {
            Log.WriteMessage(LogVerbosity.Diagnostic, String.Format(SignCheckResources.DiagDeletingFolder, path));

            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
    }
}
