using System.Collections.Generic;

namespace Microsoft.DotNet.SwaggerGenerator.LinqPad
{
    public static class KeyValuePairExtensions
    {
        public static void Deconstruct<TKey, TValue>(this KeyValuePair<TKey, TValue> that, out TKey key, out TValue value)
        {
            key = that.Key;
            value = that.Value;
        }
    }
}
