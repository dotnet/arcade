// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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

        protected bool GenerateExclusion
        {
            get
            {
                return (Options & SignatureVerificationOptions.GenerateExclusion) == SignatureVerificationOptions.GenerateExclusion;
            }
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
        /// Retrieve a FileVerifier from the SignatureVerificationManager. The retrieval is based on the file extension. If there are no
        /// applicable verifiers, the verifier is assigned based on the file's header.
        /// </summary>
        /// <param name="path">The path of the file for which to retrieve a FileVerifier.</param>
        /// <returns>A FileVerifier that can be used to verify the signature of the specified file.</returns>
        protected FileVerifier GetFileVerifier(string path)
        {
            string extension = Path.GetExtension(path);
            FileVerifier fileVerifier = SignatureVerificationManager.GetFileVerifierByExtension(extension);

            if (fileVerifier == null)
            {
                fileVerifier = SignatureVerificationManager.GetFileVerifierByHeader(path);

                if (fileVerifier == null)
                {
                    return SignatureVerificationManager.UnsupportedFileVerifier;
                }
                else
                {
                    Log.WriteMessage(LogVerbosity.Detailed, SignCheckResources.DetailIdentifyByHeaderAsType, fileVerifier.FileExtension);
                }
            }

            return fileVerifier;            
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
        /// <param name="path">The path of the file on disk to verify.</param>
        /// <param name="parent">The name of parent container, e.g. an MSI or VSIX. Can be null when there is no parent container.</param>
        /// <param name="containerPath">The path of the file in the container. This may differ from the path on disk as containers are flattened. It's
        /// primarily intended to help with exclusions and report more readable names.</param>
        /// <returns>The verification result.</returns>
        public SignatureVerificationResult VerifyFile(string path, string parent, string containerPath)
        {
            Log.WriteMessage(LogVerbosity.Detailed, String.Format(SignCheckResources.ProcessingFile, Path.GetFileName(path), String.IsNullOrEmpty(parent) ? SignCheckResources.NA : parent));

            string extension = Path.GetExtension(path);
            FileVerifier fileVerifier = GetFileVerifier(path);

            SignatureVerificationResult svr;
            svr = fileVerifier.VerifySignature(path, parent);

            CheckAndUpdateExclusions(ref svr, path, parent, containerPath);

            // Include the full path for top-level files
            if (String.IsNullOrEmpty(parent))
            {
                svr.AddDetail(DetailKeys.File, SignCheckResources.DetailFullName, svr.FullPath);
            }

            return svr;
        }

        /// <summary>
        /// Checks whether the specified file is excluded from verification and updates the SignatureVerificationResult.
        /// If <see cref="GenerateExclusion"/> is true, an exclusion entry is also generated.
        /// </summary>
        /// <param name="result">The SignatureVerificationResult to update.</param>
        /// <param name="path">The path of the file on disk.</param>
        /// <param name="parent">The parent (container) of the file.</param>
        /// <param name="containerPath">The path of the file in the container. May be null if the file is not embedded in a container.</param>
        protected void CheckAndUpdateExclusions(ref SignatureVerificationResult result, string path, string parent, string containerPath)
        {
            if (Exclusions.Count > 0)
            {
                result.IsExcluded = Exclusions.IsParentExcluded(parent) || Exclusions.IsFileExcluded(path) || Exclusions.IsFileExcluded(containerPath);

                if (result.IsExcluded)
                {
                    result.AddDetail(DetailKeys.File, SignCheckResources.DetailExcluded);
                }
            }
            if (GenerateExclusion)
            {
                result.ExclusionEntry = String.Join(";", String.Join("|", path, containerPath), parent, String.Empty);
                Log.WriteMessage(LogVerbosity.Diagnostic, SignCheckResources.DiagGenerateExclusion, result.Filename, result.ExclusionEntry);
            }
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
