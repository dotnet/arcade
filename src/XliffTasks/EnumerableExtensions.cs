using System;
using System.Collections.Generic;
using System.Text;

namespace XliffTasks
{
    internal static class EnumerableExtensions
    {
        public static bool IsSorted<T, U>(this IEnumerable<T> source, Func<T, U> keySelector, IComparer<U> comparer)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (keySelector == null)
            {
                throw new ArgumentNullException(nameof(keySelector));
            }

            if (comparer == null)
            {
                throw new ArgumentNullException(nameof(comparer));
            }

            U priorKey = default(U);
            bool first = true;

            foreach (var item in source)
            {
                U key = keySelector(item);

                if (!first
                    && comparer.Compare(priorKey, key) > 0)
                {
                    return false;
                }

                first = false;
                priorKey = key;
            }

            return true;
        }
    }
}
