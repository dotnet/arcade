using System;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Microsoft.DotNet.SetupNugetSources.Tests
{
    public static class TestNuGetConfigFactory
    {
        public static string CreateBasicConfig()
        {
            return @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <packageSources>
    <add key=""nuget.org"" value=""https://api.nuget.org/v3/index.json"" />
  </packageSources>
</configuration>";
        }

        public static string CreateConfigWithDotNetFeeds()
        {
            return @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <packageSources>
    <add key=""nuget.org"" value=""https://api.nuget.org/v3/index.json"" />
    <add key=""dotnet6"" value=""https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet6/nuget/v3/index.json"" />
    <add key=""dotnet10"" value=""https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet10/nuget/v3/index.json"" />
  </packageSources>
</configuration>";
        }

        public static string CreateConfigWithDisabledSources()
        {
            return @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <packageSources>
    <add key=""nuget.org"" value=""https://api.nuget.org/v3/index.json"" />
    <add key=""dotnet6"" value=""https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet6/nuget/v3/index.json"" />
  </packageSources>
  <disabledPackageSources>
    <add key=""darc-int-dotnet-roslyn-12345"" value=""true"" />
    <add key=""darc-int-dotnet-runtime-67890"" value=""true"" />
  </disabledPackageSources>
</configuration>";
        }

        public static string CreateConfigWithCredentials()
        {
            return @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <packageSources>
    <add key=""nuget.org"" value=""https://api.nuget.org/v3/index.json"" />
    <add key=""dotnet6"" value=""https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet6/nuget/v3/index.json"" />
  </packageSources>
  <packageSourceCredentials>
    <dotnet6-internal>
      <add key=""Username"" value=""dn-bot"" />
      <add key=""ClearTextPassword"" value=""existing-token"" />
    </dotnet6-internal>
  </packageSourceCredentials>
</configuration>";
        }

        public static string CreateEmptyConfig()
        {
            return @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
</configuration>";
        }

        public static string CreateConfigWithoutPackageSources()
        {
            return @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <disabledPackageSources>
    <add key=""darc-int-dotnet-runtime-67890"" value=""true"" />
  </disabledPackageSources>
</configuration>";
        }

        public static string CreateConfigWithMultipleDotNetVersions()
        {
            return @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <packageSources>
    <add key=""nuget.org"" value=""https://api.nuget.org/v3/index.json"" />
    <add key=""dotnet5"" value=""https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet5/nuget/v3/index.json"" />
    <add key=""dotnet6"" value=""https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet6/nuget/v3/index.json"" />
    <add key=""dotnet7"" value=""https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet7/nuget/v3/index.json"" />
    <add key=""dotnet8"" value=""https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet8/nuget/v3/index.json"" />
    <add key=""dotnet9"" value=""https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet9/nuget/v3/index.json"" />
    <add key=""dotnet10"" value=""https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet10/nuget/v3/index.json"" />
  </packageSources>
</configuration>";
        }
    }
}
