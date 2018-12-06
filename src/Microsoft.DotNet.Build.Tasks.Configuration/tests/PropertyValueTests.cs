using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Microsoft.DotNet.Build.Tasks.Configuration.Tests
{
    public class PropertyValueTests
    {

        [Theory]
        [InlineData("Linux", "OSGroup", "Unix;Somenix", "Compatnix")]
        [InlineData("Linux", "OSGroup", "Unix", "")]
        [InlineData("Linux", "OSGroup", "", "Compatnix")]
        [InlineData("Linux", "OSGroup", "", "")]
        public void Create(string value, string propertyName, string imports, string compatibleWith)
        {
            var propertyInfo = new PropertyInfo(Items.CreateProperty(propertyName, "", "0", "0"));
            var properties = new Dictionary<string, PropertyInfo>() { { propertyName, propertyInfo } };
            var propertyValue = new PropertyValue(Items.CreatePropertyValue(value, propertyName, imports, compatibleWith), properties);
            Assert.Equal(value, propertyValue.Value);
            Assert.Equal(propertyInfo, propertyValue.Property);

            Dictionary<string, PropertyValue> values = new Dictionary<string, PropertyValue>();

            values.Add(value, propertyValue);

            var importValues = CreateValues(imports);

            var compatibleValues = CreateValues(compatibleWith);

            propertyValue.ConnectValues(values);

            Assert.Equal(importValues, propertyValue.ImportValues);
            Assert.Equal(compatibleValues, propertyValue.CompatibleValues);

            return;

            PropertyValue[] CreateValues(string metadataValue)
            {
                if (string.IsNullOrEmpty(metadataValue))
                {
                    return Array.Empty<PropertyValue>();
                }

                return metadataValue.Split(';')
                        .Select(valueString =>
                            AddValue(new PropertyValue(Items.CreatePropertyValue(valueString, propertyName, "", ""), properties)))
                        .ToArray();
            }


            PropertyValue AddValue(PropertyValue newValue)
            {
                values.Add(newValue.Value, newValue);
                return newValue;
            }

        }

    }
}
