using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.DotNet.Build.Tasks.Configuration.Tests
{
    internal class Items
    {
        public static ITaskItem CreateProperty(string name, string defaultValue, string order, string precedence)
        {
            var item = new TaskItem(name);
            item.SetMetadata(nameof(PropertyInfo.DefaultValue), defaultValue);
            item.SetMetadata(nameof(PropertyInfo.Order), order);
            item.SetMetadata(nameof(PropertyInfo.Precedence), precedence);
            return item;
        }

        public static ITaskItem CreatePropertyValue(string value, string propertyName, string imports = null, string compatibleWith = null)
        {
            var item = new TaskItem(value);
            item.SetMetadata("Property", propertyName);
            item.SetMetadata("Imports", imports);
            item.SetMetadata("CompatibleWith", compatibleWith);
            return item;
        }
    }
}
