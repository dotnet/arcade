// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace Microsoft.DotNet.Build.Tasks.Workloads
{
    /// <summary>
    /// Extension methods for ICollection.
    /// </summary>
    public static class ICollectionExtensions
    {
        /// <summary>
        /// Adds the elements of the source collection to the end of the destination collection.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="destination">The collection to modify.</param>
        /// <param name="source">The collection to add.</param>
        public static void AddRange<T>(this ICollection<T> destination, IEnumerable<T> source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            foreach (T item in source)
            {
                destination.Add(item);
            }
        }
    }
}
