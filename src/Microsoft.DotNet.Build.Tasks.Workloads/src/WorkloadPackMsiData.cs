using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Build.Tasks.Workloads
{
    /// <summary>
    /// Describes all the data required to generate an MSI from a workload pack NuGet package.
    /// </summary>
    public class WorkloadPackMsiData
    {
        public string SourcePackage
        {
            get;
            set;
        }

        public string Platform
        {
            get;
            set;
        }

        public string[] Platforms
        {
            get;
            set;
        }

        public string OutputPath
        {
            get;
            set;
        }

        public string InstallDir
        {
            get;
            set;
        }

        public override bool Equals(object obj)
        {
            if ((obj is null) || !GetType().Equals(obj.GetType()))
            {
                return false;
            }

            WorkloadPackMsiData other = (WorkloadPackMsiData)obj;
            return string.Equals(Platform, other.Platform) && (string.Equals(SourcePackage, other.SourcePackage));
        }

        public override int GetHashCode()
        {
            return SourcePackage.GetHashCode() ^ Platform.GetHashCode();
        }
    }
}
