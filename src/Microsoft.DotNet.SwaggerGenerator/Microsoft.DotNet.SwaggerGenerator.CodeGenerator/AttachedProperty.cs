using System.Runtime.CompilerServices;
using Swashbuckle.AspNetCore.Swagger;

namespace SwaggerGenerator
{
    public class AttachedProperty<TKey, TValue>
        where TKey : class
        where TValue : class
    {
        private readonly ConditionalWeakTable<TKey, TValue> _table = new ConditionalWeakTable<TKey, TValue>();

        public void Set(TKey key, TValue value)
        {
            if (_table.TryGetValue(key, out var v))
            {
                _table.Remove(key);
            }
            _table.Add(key, value);
        }

        public TValue Get(TKey key)
        {
            _table.TryGetValue(key, out TValue value);
            return value;
        }

        public TValue GetOrAdd(TKey key, ConditionalWeakTable<TKey, TValue>.CreateValueCallback createValue)
        {
            return _table.GetValue(key, createValue);
        }
    }
}