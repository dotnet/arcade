// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Microsoft.DotNet.Build.Tasks.Configuration
{
    /// <summary>
    /// An ordered collection of property values
    /// </summary>
    public class Configuration
    {
        public Configuration(PropertyValue[] values)
        {
            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            Values = values;
        }

        public PropertyValue[] Values { get; }

        public static IEqualityComparer<Configuration> CompatibleComparer { get; } = new CompatibleConfigurationComparer();

        public bool IsPlaceHolderConfiguration { get; set; }

        [Flags]
        private enum ConfigurationValueCategories
        {
            Significant           = 0b000001,
            SignificantDefault    = 0b000010,
            Insignificant         = 0b000100,
            InsignificantDefault  = 0b001000,
            Independent           = 0b010000,
            IndependentDefault    = 0b100000
        }

        /// <summary>
        /// Constructs a configuration string from this configuration
        /// </summary>
        /// <param name="allowDefaults">true to omit default values from configuration string</param>
        /// <param name="encounteredDefault">true if a default value was omitted</param>
        /// <returns>configuration string</returns>
        private string GetConfigurationString(ConfigurationValueCategories categories, out bool encounteredDefault)
        {
            encounteredDefault = false;
            var configurationBuilder = new StringBuilder();
            foreach (var value in Values)
            {
                bool isDefault = value == value.Property.DefaultValue;

                ConfigurationValueCategories currentCategory;

                if (value.Property.Independent)
                {
                    currentCategory = isDefault ? ConfigurationValueCategories.IndependentDefault : ConfigurationValueCategories.Independent;
                }
                else if (value.Property.Insignificant)
                {
                    currentCategory = isDefault ? ConfigurationValueCategories.InsignificantDefault : ConfigurationValueCategories.Insignificant;
                }
                else
                {
                    currentCategory = isDefault ? ConfigurationValueCategories.SignificantDefault : ConfigurationValueCategories.Significant;

                }

                if ((categories & currentCategory) != currentCategory)
                {
                    encounteredDefault |= isDefault;
                    continue;
                }

                if (configurationBuilder.Length > 0)
                {
                    configurationBuilder.Append(ConfigurationFactory.PropertySeparator);
                }
                configurationBuilder.Append(value.Value);
            }

            return configurationBuilder.ToString();
        }

        /// <summary>
        /// Get properties associated with this configuration
        /// </summary>
        /// <returns></returns>
        public IDictionary<string, string> GetProperties()
        {
            Dictionary<string, string> properties = new Dictionary<string, string>();

            foreach (var value in Values)
            {
                properties.Add(value.Property.Name, value.Value);

                foreach (var additionalProperty in value.AdditionalProperties)
                {
                    properties.Add(additionalProperty.Key, additionalProperty.Value);
                }
            }

            return properties;
        }

        public IEnumerable<string> GetConfigurationStrings()
        {
            bool encounteredDefault = false;

            yield return GetConfigurationString(ConfigurationValueCategories.Significant |
                                                ConfigurationValueCategories.Insignificant,
                                                out encounteredDefault);
            if (encounteredDefault)
            {
                yield return GetConfigurationString(ConfigurationValueCategories.Significant | ConfigurationValueCategories.SignificantDefault |
                                                    ConfigurationValueCategories.Insignificant | ConfigurationValueCategories.InsignificantDefault,
                                                    out encounteredDefault);
            }
        }

        public IEnumerable<string> GetSignificantConfigurationStrings()
        {
            bool encounteredDefault = false;

            yield return GetConfigurationString(ConfigurationValueCategories.Significant,
                                                out encounteredDefault);

            if (encounteredDefault)
            {
                yield return GetConfigurationString(ConfigurationValueCategories.Significant | ConfigurationValueCategories.SignificantDefault,
                                                    out encounteredDefault);
            }
        }

        public string GetDefaultConfigurationString()
        {
            return GetConfigurationString(ConfigurationValueCategories.Significant | ConfigurationValueCategories.SignificantDefault |
                                          ConfigurationValueCategories.Insignificant | ConfigurationValueCategories.InsignificantDefault,
                                          out bool unused);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            var other = obj as Configuration;

            if (other == null)
            {
                return false;
            }

            if (Values.Length != other.Values.Length)
            {
                return false;
            }
            return Values.SequenceEqual(other.Values);
        }

        private Nullable<int> hashCode;
        private Nullable<int> compatibleHashCode;
        public override int GetHashCode()
        {
            if (hashCode == null)
            {
                hashCode = 0;
                foreach (var value in Values)
                {
                    hashCode ^= value.GetHashCode();
                }
            }
            return hashCode.Value;
        }

        // Only examines significant properties for equality
        private class CompatibleConfigurationComparer : IEqualityComparer<Configuration>
        {
            public bool Equals(Configuration x, Configuration y)
            {
                if (ReferenceEquals(x, y))
                {
                    return true;
                }

                if (x == null || y == null)
                {
                    return false;
                }

                var xValues = x.Values.Where(v => !v.Property.Insignificant);
                var yValues = y.Values.Where(v => !v.Property.Insignificant);

                return xValues.SequenceEqual(yValues);
            }

            public int GetHashCode(Configuration obj)
            {
                if (obj.compatibleHashCode == null)
                {
                    obj.compatibleHashCode = 0;
                    foreach (var value in obj.Values)
                    {
                        if (!value.Property.Insignificant)
                        {
                            obj.compatibleHashCode ^= value.GetHashCode();
                        }
                    }
                }
                return obj.compatibleHashCode.Value;
            }
        }

        public override string ToString()
        {
            return GetConfigurationString(ConfigurationValueCategories.Significant |
                                          ConfigurationValueCategories.Insignificant,
                                          out bool unused);
        }

        /// <summary>
        /// Returns a string that includes insignificant values
        /// </summary>
        public string ToFullString()
        {
            return GetConfigurationString(ConfigurationValueCategories.Significant |
                                          ConfigurationValueCategories.Insignificant | ConfigurationValueCategories.InsignificantDefault,
                                          out bool unused);
        }
    }
}
