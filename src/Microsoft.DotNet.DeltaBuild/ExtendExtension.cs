// Copyright (c) Microsoft Corporation. All Rights Reserved.

using System.Collections.Generic;

namespace DeltaBuild;

internal static class ExtendExtension
{
    public static void Extend<TFrom, TTo>(
        this IDictionary<TFrom, HashSet<TTo>> dict,
        TFrom fromItem,
        IEnumerable<TTo> toItems)
    {
        var toItemsTotal = 0;
        foreach (var toItem in toItems)
        {
            toItemsTotal++;
            if (!dict.ContainsKey(fromItem))
            {
                dict.Add(fromItem, new HashSet<TTo>());
            }

            if (!dict[fromItem].Contains(toItem))
            {
                dict[fromItem].Add(toItem);
            }
        }

        if (toItemsTotal == 0 && !dict.ContainsKey(fromItem))
        {
            // If there are no <to> items, create an empty set.
            dict.Add(fromItem, new HashSet<TTo>());
        }
    }

    public static void Extend<TTo, TFrom>(
        this IDictionary<TFrom, HashSet<TTo>> dict,
        IEnumerable<TFrom> fromItems,
        TTo toItem)
    {
        foreach (var fromItem in fromItems)
        {
            if (!dict.ContainsKey(fromItem))
            {
                dict.Add(fromItem, new HashSet<TTo>());
            }

            if (!dict[fromItem].Contains(toItem))
            {
                dict[fromItem].Add(toItem);
            }
        }
    }
}
