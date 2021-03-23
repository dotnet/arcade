// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Text;

namespace Microsoft.DotNet.Build.Tasks.Packaging
{
    internal class RID
    {
        internal const char VersionDelimiter = '.';
        internal const char ArchitectureDelimiter = '-';
        internal const char QualifierDelimiter = '-';

        public string BaseRID { get; set; }
        public bool OmitVersionDelimiter { get; set; }
        public RuntimeVersion Version { get; set; }
        public string Architecture { get; set; }
        public string Qualifier { get; set; }

        public override string ToString()
        {
            StringBuilder builder = new StringBuilder(BaseRID);

            if (HasVersion())
            {
                if (!OmitVersionDelimiter)
                {
                    builder.Append(VersionDelimiter);
                }
                builder.Append(Version);
            }

            if (HasArchitecture())
            {
                builder.Append(ArchitectureDelimiter);
                builder.Append(Architecture);
            }

            if (HasQualifier())
            {
                builder.Append(QualifierDelimiter);
                builder.Append(Qualifier);
            }

            return builder.ToString();
        }


        enum RIDPart : int
        {
            Base = 0,
            Version,
            Architcture,
            Qualifier,
            Max = Qualifier
        }

        public static RID Parse(string runtimeIdentifier)
        {
            string[] parts = new string[(int)RIDPart.Max + 1];
            bool omitVersionDelimiter = true;
            RIDPart parseState = RIDPart.Base;

            int partStart = 0, partLength = 0;

            // qualifier is indistinguishable from arch so we cannot distinguish it for parsing purposes
            Debug.Assert(ArchitectureDelimiter == QualifierDelimiter);

            for (int i = 0; i < runtimeIdentifier.Length; i++)
            {
                char current = runtimeIdentifier[i];
                partLength = i - partStart;

                switch (parseState)
                {
                    case RIDPart.Base:
                        // treat any number as the start of the version
                        if (current == VersionDelimiter || (current >= '0' && current <= '9'))
                        {
                            SetPart();
                            partStart = i;
                            if (current == VersionDelimiter)
                            {
                                omitVersionDelimiter = false;
                                partStart = i + 1;
                            }
                            parseState = RIDPart.Version;
                        }
                        // version might be omitted
                        else if (current == ArchitectureDelimiter)
                        {
                            // ensure there's no version later in the string
                            if (-1 != runtimeIdentifier.IndexOf(VersionDelimiter, i))
                            {
                                break;
                            }
                            SetPart();
                            partStart = i + 1;  // skip delimiter
                            parseState = RIDPart.Architcture;
                        }
                        break;
                    case RIDPart.Version:
                        if (current == ArchitectureDelimiter)
                        {
                            SetPart();
                            partStart = i + 1;  // skip delimiter
                            parseState = RIDPart.Architcture;
                        }
                        break;
                    case RIDPart.Architcture:
                        if (current == QualifierDelimiter)
                        {
                            SetPart();
                            partStart = i + 1;  // skip delimiter
                            parseState = RIDPart.Qualifier;
                        }
                        break;
                    default:
                        break;
                }
            }

            partLength = runtimeIdentifier.Length - partStart;
            if (partLength > 0)
            {
                SetPart();
            }

            string GetPart(RIDPart part)
            {
                return parts[(int)part];
            }

            void SetPart()
            {
                if (partLength == 0)
                {
                    throw new ArgumentException($"unexpected delimiter at position {partStart} in {runtimeIdentifier}");
                }

                parts[(int)parseState] = runtimeIdentifier.Substring(partStart, partLength);
            }

            string version = GetPart(RIDPart.Version);

            if (version == null)
            {
                omitVersionDelimiter = false;
            }

            return new RID()
            {
                BaseRID = GetPart(RIDPart.Base),
                OmitVersionDelimiter = omitVersionDelimiter,
                Version = version == null ? null : new RuntimeVersion(version),
                Architecture = GetPart(RIDPart.Architcture),
                Qualifier = GetPart(RIDPart.Qualifier)
            };
        }


        public bool HasVersion()
        {
            return Version != null;
        }

        public bool HasArchitecture()
        {
            return Architecture != null;
        }

        public bool HasQualifier()
        {
            return Qualifier != null;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as RID);
        }

        public bool Equals(RID obj)
        {
            return object.ReferenceEquals(obj, this) ||
                (!(obj is null) &&
                BaseRID == obj.BaseRID &&
                OmitVersionDelimiter == obj.OmitVersionDelimiter &&
                Version == obj.Version &&
                Architecture == obj.Architecture &&
                Qualifier == obj.Qualifier);

        }

        public override int GetHashCode()
        {
#if NETFRAMEWORK
            return BaseRID.GetHashCode();
#else
            HashCode hashCode = new HashCode();
            hashCode.Add(BaseRID);
            hashCode.Add(VersionDelimiter);
            hashCode.Add(Version);
            hashCode.Add(ArchitectureDelimiter);
            hashCode.Add(Architecture);
            hashCode.Add(QualifierDelimiter);
            hashCode.Add(Qualifier);
            return hashCode.ToHashCode();
#endif
        }
    }
}
