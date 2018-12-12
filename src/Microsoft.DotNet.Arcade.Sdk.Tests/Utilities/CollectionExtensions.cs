using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Microsoft.DotNet.Arcade.Sdk.Tests
{
    public static class CollectionExtensions
    {
        public static Collection<T> AddRange<T>(this Collection<T> collection, IEnumerable<T> items)
        {
            foreach(var item in items)
            {
                collection.Add(item);
            }
            return collection;
        }
    }
}
