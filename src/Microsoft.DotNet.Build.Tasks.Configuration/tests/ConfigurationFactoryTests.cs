using Microsoft.Build.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;

namespace Microsoft.DotNet.Build.Tasks.Configuration.Tests
{
    public class ConfigurationFactoryTests
    {
        [Fact]
        public void EmptyFactory()
        {
            var factory = new ConfigurationFactory(Array.Empty<ITaskItem>(), Array.Empty<ITaskItem>());

            Assert.Single(factory.GetAllConfigurations());
            Assert.Single(factory.GetConfigurations(property => Enumerable.Empty<PropertyValue>()));
            Assert.Empty(factory.GetProperties());
            Assert.Single(factory.GetSignificantConfigurations());
            Assert.NotNull(factory.IdentityConfiguration);
            Assert.Empty(factory.IdentityConfiguration.Values);
        }

        private ConfigurationFactory CreateConfigurationFactory()
        {
            var osGroup = "OSGroup";
            var targetGroup = "TargetGroup";
            var configurationGroup = "ConfigurationGroup";
            var archGroup = "ArchGroup";
            var properties = new[]
            {
                Items.CreateProperty(osGroup, "AnyOS", order:"2", precedence:"1"),
                Items.CreateProperty(targetGroup, "", order:"1", precedence:"2"),
                Items.CreateProperty(configurationGroup, "Debug", order:"3", precedence:"Insignificant"),
                Items.CreateProperty(archGroup, "x64", order:"4", precedence:"Independent")
            };

            var propertyValues = new[]
            {
                Items.CreatePropertyValue("AnyOS", osGroup),
                Items.CreatePropertyValue("Windows_NT", osGroup, "AnyOS"),
                Items.CreatePropertyValue("Unix", osGroup, "AnyOS"),
                Items.CreatePropertyValue("Linux", osGroup, "Unix"),
                Items.CreatePropertyValue("OSX", osGroup, "Unix"),

                // minimal set of targetgroups
                
                Items.CreatePropertyValue("netfx", targetGroup, "net472", "netstandard"),
                Items.CreatePropertyValue("net472", targetGroup, "net471", "netstandard"),
                Items.CreatePropertyValue("net471", targetGroup, "net461", "netstandard"),
                Items.CreatePropertyValue("net461", targetGroup, "net46", "netstandard"),
                Items.CreatePropertyValue("net46", targetGroup, "net451", "netstandard1.3"),
                Items.CreatePropertyValue("net451", targetGroup, "net45", "netstandard1.2"),
                Items.CreatePropertyValue("net45", targetGroup, "", "netstandard1.1"),


                Items.CreatePropertyValue("netcoreapp", targetGroup, "netcoreapp3.0", "netstandard"),
                Items.CreatePropertyValue("netcoreapp3.0", targetGroup, "netcoreapp2.2", "netstandard"),
                Items.CreatePropertyValue("netcoreapp2.2", targetGroup, "netcoreapp2.1", "netstandard"),
                Items.CreatePropertyValue("netcoreapp2.1", targetGroup, "netcoreapp2.0", "netstandard"),
                Items.CreatePropertyValue("netcoreapp2.0", targetGroup, "netcoreapp1.1", "netstandard"),
                Items.CreatePropertyValue("netcoreapp1.1", targetGroup, "netcoreapp1.0", "netstandard1.6"),
                Items.CreatePropertyValue("netcoreapp1.0", targetGroup, "", "netstandard1.6"),

                Items.CreatePropertyValue("netstandard", targetGroup, "netstandard2.0"),
                Items.CreatePropertyValue("netstandard2.0", targetGroup, "netstandard1.6"),
                Items.CreatePropertyValue("netstandard1.6", targetGroup, "netstandard1.5"),
                Items.CreatePropertyValue("netstandard1.5", targetGroup, "netstandard1.4"),
                Items.CreatePropertyValue("netstandard1.4", targetGroup, "netstandard1.3"),
                Items.CreatePropertyValue("netstandard1.3", targetGroup, "netstandard1.2"),
                Items.CreatePropertyValue("netstandard1.2", targetGroup, "netstandard1.1"),
                Items.CreatePropertyValue("netstandard1.1", targetGroup, "netstandard1.0"),
                Items.CreatePropertyValue("netstandard1.0", targetGroup),

                // configuration groups
                Items.CreatePropertyValue("Debug", configurationGroup),
                Items.CreatePropertyValue("Release", configurationGroup),

                // arch groups
                Items.CreatePropertyValue("x86", archGroup),
                Items.CreatePropertyValue("x64", archGroup),
                Items.CreatePropertyValue("arm", archGroup),
                Items.CreatePropertyValue("arm64", archGroup),
                Items.CreatePropertyValue("armel", archGroup)
            };

            return new ConfigurationFactory(properties, propertyValues);
        }


        [Fact]
        public void CreateFactory()
        {
            // creates a factory that approximates the CoreFx usage
            var factory = CreateConfigurationFactory();
        }

        [Fact]
        public void CanGetAllConfigurations()
        {
            var factory = CreateConfigurationFactory();

            var configurations = factory.GetAllConfigurations();

            Assert.NotEmpty(configurations);
            Assert.All(configurations, config =>
            {
                Assert.NotNull(config);
                Assert.False(config.IsPlaceHolderConfiguration);
                Assert.NotNull(config.ToString());


                var allConfigStrings = config.GetConfigurationStrings();
                Assert.NotEmpty(allConfigStrings);
                Assert.All(allConfigStrings, cs => Assert.Equal(config, factory.ParseConfiguration(cs), Configuration.CompatibleComparer));

                var defaultConfigString = config.GetDefaultConfigurationString();
                Assert.NotNull(defaultConfigString);
                Assert.Equal(config, factory.ParseConfiguration(defaultConfigString), Configuration.CompatibleComparer);

                Assert.Contains(allConfigStrings, cs => cs == defaultConfigString);
                var significantConfigStrings = config.GetSignificantConfigurationStrings();
                Assert.All(significantConfigStrings, cs => Assert.Equal(config, factory.ParseConfiguration(cs), Configuration.CompatibleComparer));
            });
        }

        [Theory]
        [InlineData("netcoreapp", "netstandard")]
        [InlineData("netcoreapp2.0", "netstandard")]
        [InlineData("netcoreapp-Windows_NT", "netstandard")]
        [InlineData("netcoreapp-Windows_NT", "netstandard-Windows_NT")]
        [InlineData("netcoreapp-Windows_NT", "netstandard-x86")]
        [InlineData("netcoreapp-Windows_NT", "netstandard-Debug")]
        [InlineData("netcoreapp-Windows_NT", "netstandard-x86-Debug")]
        [InlineData("netcoreapp", "netcoreapp2.2")]
        [InlineData("netcoreapp2.2", "netcoreapp2.1")]
        public void AreCompatible(string consuming, string referenced)
        {
            var factory = CreateConfigurationFactory();

            var consumingConfig = factory.ParseConfiguration(consuming);
            var referencedConfig = factory.ParseConfiguration(referenced);

            var compatibleConfigs = factory.GetCompatibleConfigurations(consumingConfig);

            Assert.Contains(compatibleConfigs, c => Configuration.CompatibleComparer.Equals(c, referencedConfig));
        }
    }
}
