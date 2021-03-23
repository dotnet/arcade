// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.DotNet.Build.Tasks.Packaging
{

    /// <summary>
    /// A Version class that also supports a single integer (major only)
    /// </summary>
    internal sealed class RuntimeVersion : IComparable, IComparable<RuntimeVersion>, IEquatable<RuntimeVersion>
    {
        private Version version;
        private bool hasMinor;

        public RuntimeVersion(string versionString)
        {
            // intentionally don't support the type of version that omits the separators as it is abiguous.
            // for example Windows 8.1 was encoded as win81, where as Windows 10.0 was encoded as win10

            if (versionString.IndexOf('.') == -1)
            {
                versionString += ".0";
                hasMinor = false;
            }
            else
            {
                hasMinor = true;
            }
            version = Version.Parse(versionString);

        }

        public int CompareTo(object obj)
        {
            if (obj == null)
            {
                return 1;
            }

            if (obj is RuntimeVersion version)
            {
                return CompareTo(version);
            }

            throw new ArgumentException();
        }

        public int CompareTo(RuntimeVersion other)
        {
            int versionResult = version.CompareTo(other.version);

            if (versionResult == 0)
            {
                if (!hasMinor && other.hasMinor)
                {
                    return -1;
                }

                if (hasMinor && !other.hasMinor)
                {
                    return 1;
                }
            }

            return versionResult;
        }

        public bool Equals(RuntimeVersion other)
        {
            return object.ReferenceEquals(other, this) ||
                (!(other is null) &&
                (hasMinor == other.hasMinor) &&
                version.Equals(other.version));
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as RuntimeVersion);
        }

        public override int GetHashCode()
        {
            return version.GetHashCode() | (hasMinor ? 1 : 0);
        }

        public override string ToString()
        {
            return hasMinor ? version.ToString() : version.Major.ToString();
        }

        public static bool operator ==(RuntimeVersion v1, RuntimeVersion v2)
        {
            if (v2 is null)
            {
                return (v1 is null) ? true : false;
            }

            return ReferenceEquals(v2, v1) ? true : v2.Equals(v1);
        }

        public static bool operator !=(RuntimeVersion v1, RuntimeVersion v2) => !(v1 == v2);

        public static bool operator <(RuntimeVersion v1, RuntimeVersion v2)
        {
            if (v1 is null)
            {
                return !(v2 is null);
            }

            return v1.CompareTo(v2) < 0;
        }

        public static bool operator <=(RuntimeVersion v1, RuntimeVersion v2)
        {
            if (v1 is null)
            {
                return true;
            }

            return v1.CompareTo(v2) <= 0;
        }

        public static bool operator >(RuntimeVersion v1, RuntimeVersion v2) => v2 < v1;

        public static bool operator >=(RuntimeVersion v1, RuntimeVersion v2) => v2 <= v1;
    }
}
