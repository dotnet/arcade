using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace Microsoft.DotNet.Build.Tasks.Configuration.Tests
{
    public class PropertyInfoTests
    {
        [Theory]
        [InlineData("OSGroup", "AnyOS", "1", "2")]
        [InlineData("OSGroup", "AnyOS", "100", "0")]
        [InlineData("OSGroup", "AnyOS", "1", "-2")]
        [InlineData("OSGroup", "AnyOS", "1", nameof(PropertyInfo.Insignificant))]
        [InlineData("OSGroup", "AnyOS", "1", nameof(PropertyInfo.Independent))]
        public void Create(string name, string defaultValue, string order, string precedence)
        {
            var propInfo = new PropertyInfo(Items.CreateProperty(name, defaultValue, order, precedence));

            Assert.Equal(name, propInfo.Name);
            Assert.Equal(int.Parse(order), propInfo.Order);

            if (precedence == nameof(PropertyInfo.Independent))
            {
                Assert.Equal(int.MaxValue, propInfo.Precedence);
                Assert.True(propInfo.Independent);
                Assert.True(propInfo.Insignificant);
            }
            else if (precedence == nameof(PropertyInfo.Insignificant))
            {
                Assert.Equal(int.MaxValue, propInfo.Precedence);
                Assert.True(propInfo.Insignificant);
            }
            else
            {
                var expectedPrecedence = int.Parse(precedence);
                Assert.Equal(expectedPrecedence, propInfo.Precedence);
            }

            Assert.Null(propInfo.DefaultValue);

            var defaultPropValue = new PropertyValue(defaultValue, propInfo);
            propInfo.ConnectDefault(new Dictionary<string, PropertyValue>() { { defaultValue, defaultPropValue } });

            Assert.Equal(defaultPropValue, propInfo.DefaultValue);
             
            var otherPropInfo = new PropertyInfo(Items.CreateProperty(name, defaultValue, order, precedence));
            Assert.Equal(propInfo, otherPropInfo);
        }

        [Theory]
        [InlineData("OSGroup", "AnyOS", "1.0", "0")]
        [InlineData("OSGroup", "AnyOS", "null", "0")]
        [InlineData("OSGroup", "AnyOS", "", "0")]
        [InlineData("OSGroup", "AnyOS", "1", "car")]
        [InlineData("OSGroup", "AnyOS", "1", "1E45")]
        [InlineData("OSGroup", "AnyOS", "1", "")]
        public void ThrowsInvalidDataException(string name, string defaultValue, string order, string precedence)
        {
            Assert.Throws<InvalidDataException>(() => new PropertyInfo(Items.CreateProperty(name, defaultValue, order, precedence)));
        }

        [Fact]
        public void InvalidDefaults()
        {
            string defaultValue = "myValue";
            var propInfo = new PropertyInfo(Items.CreateProperty("MyProp", defaultValue, "0", "0"));

            var values = new Dictionary<string, PropertyValue>();

            // DefaultValue is defined
            Assert.Throws<ArgumentException>(() => propInfo.ConnectDefault(values));

            // DefaultValue is defined by mapped to a different property
            var propInfo2 = new PropertyInfo(Items.CreateProperty("MyProp2", defaultValue, "0", "0"));
            values.Add(defaultValue, new PropertyValue(defaultValue, propInfo2));
            Assert.Throws<ArgumentException>(() => propInfo.ConnectDefault(values));
        }

    }
}
