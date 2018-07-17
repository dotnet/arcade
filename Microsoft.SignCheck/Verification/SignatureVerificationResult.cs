using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.SignCheck.Verification
{
    public class SignatureVerificationResult
    {
        private string _tempPath;
        private List<SignatureVerificationResult> _nestedResults;

        /// <summary>
        /// A string containing the details of a verificaiton operation.
        /// </summary>
        public string Detail
        {
            get;
            private set;
        }

        /// <summary>
        /// A string containing the formatted <see cref="Exclusion"/> entry that would allow the file to be excluded from verification.
        /// </summary>
        public string ExclusionEntry
        {
            get;
            private set;
        }

        public string Filename
        {
            get;
            set;
        }

        public string FullPath
        {
            get;
            set;
        }

        /// <summary>
        /// True if the file was excluded from verification, false otherwise.
        /// </summary>
        public bool IsExcluded
        {
            get;
            set;
        }

        /// <summary>
        /// True if the file contains a valid signature, false otherwise.
        /// </summary>
        public bool IsSigned
        {
            get;
            set;
        }
        
        /// <summary>
        /// True if signature verification was skipped, false otherwise. Files are skipped when the file type (base on the extension) is unknown.
        /// </summary>
        public bool IsSkipped
        {
            get;
            set;
        }

        /// <summary>
        /// A set of results for nested files. For example, if recursive verification is enabled and a file contains embedded files, e.g. an MSI, then
        /// this property will contain the verification results of the embedded files.
        /// </summary>
        public ICollection<SignatureVerificationResult> NestedResults
        {
            get
            {
                if (_nestedResults == null)
                {
                    _nestedResults = new List<SignatureVerificationResult>();
                }

                return _nestedResults;
            }
            set
            {
                _nestedResults = value.ToList();
            }
        }

        public string TempPath
        {
            get
            {
                if (String.IsNullOrEmpty(_tempPath))
                    {
                    _tempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                }

                return _tempPath;
            }
        }

        public SignatureVerificationResult(string path, Dictionary<string, Exclusion> exclusions, string parent)
        {
            if (String.IsNullOrEmpty(path))
            {
                throw new ArgumentException("path");
            }

            Filename = Path.GetFileName(path);
            FullPath = Path.GetFullPath(path);
            AddDetail(SignCheckResources.DetailFile, Filename);

            ExclusionEntry = String.Join(";", Filename, parent, SignCheckResources.ExclusionYourComment);

            if (exclusions != null)
            {
                string exclusionComment = SignCheckResources.NA;
                if (Exclusion.IsExcluded(Filename, exclusions, parent, out exclusionComment))
                {
                    IsExcluded = true;
                    AddDetail(SignCheckResources.DetailExcluded, exclusionComment);
                }
            }
        }

        public void AddDetail(string detail)
        {
            if (String.IsNullOrEmpty(Detail))
            {
                Detail = detail;
            }
            else
            {
                Detail += ", " + detail;
            }
        }

        public void AddDetail(string detail, params object[] values)
        {
            AddDetail(String.Format(detail, values));
        }

        /// <summary>
        /// Creates a <see cref="SignatureVerificationResult"/> for a file to indicate that verification was skipped.
        /// </summary>
        /// <param name="path">The path to the file that was skipped.</param>
        /// <returns>A <see cref="SignatureVerificationResult"/> that indicates the file verification was skipped.</returns>
        public static SignatureVerificationResult SkippedResult(string path)
        {
            var signatureVerificationResult = new SignatureVerificationResult(path, exclusions: null, parent: null)
            {
                IsSkipped = true
            };

            signatureVerificationResult.AddDetail(SignCheckResources.DetailSigned, SignCheckResources.DetailSkippedUnsupportedFileType);

            return signatureVerificationResult;
        }
    }
}
