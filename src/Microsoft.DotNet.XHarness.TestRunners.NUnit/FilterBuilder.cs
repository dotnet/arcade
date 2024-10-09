// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using NUnit.Engine;

#nullable enable
namespace Microsoft.DotNet.XHarness.TestRunners.NUnit;

/// <summary>
/// Helper class that will build an NUnit v3 filter
/// </summary>
internal class FilterBuilder
{
    private readonly ITestFilterBuilder _testFilterBuilder;
    private readonly bool _runAssemblyByDefault;

    public List<string> IgnoredCategories { get; } = new List<string>();
    public List<string> IgnoredClasses { get; } = new List<string>();
    public List<string> IgnoredMethods { get; } = new List<string>();

    public FilterBuilder(ITestFilterBuilder testFilterBuilder, bool runAssemblyByDefault = true)
    {
        _testFilterBuilder = testFilterBuilder ?? throw new ArgumentNullException(nameof(testFilterBuilder));
        _runAssemblyByDefault = runAssemblyByDefault;
    }

    internal string? BuildWhereClause() // does not need to be internal, doing it so that we can test it
    {
        // not the best api to add tests, we need to build the expression to be passed to the filter builder
        // to generate the tests. First, consider the case in which we are running all the tests, in that
        // case we want to make sure that we are || and ==
        // For example:
        // test == "My.Skipped.Test" or cat == "outerloop"
        //
        // the above filter will skip any test that matches the full qualify named and the category outerloop.
        // On the other hand, when we are including them, we have to do the reverse:
        // test != "My.Skipped.Test" or cat != "outerloop"
        //
        // The above filter will skip all tests that do not have the given fully-qualified name of the category
        // build the comparison tuples, then use string.join with the or operation
        var comparisons = new List<string>();
        var filters = new Dictionary<string, List<string>>
        {
            ["cat"] = IgnoredCategories,
            ["class"] = IgnoredClasses,
            ["test"] = IgnoredMethods, // we could use AddTest, but it will not allow us to do the !isExcluded
        };

        foreach (string category in filters.Keys)
        {
            var filtersInCategory = filters[category];
            foreach (var filterReason in filtersInCategory)
            {
                var eq = _runAssemblyByDefault ? "==" : "!=";
                comparisons.Add($"{category} {eq} {filterReason}");
            }
        }
        return comparisons.Count == 0 ? null : string.Join(" or ", comparisons);
    }

    public TestFilter GetFilter()
    {
        var whereClause = BuildWhereClause();
        if (!string.IsNullOrEmpty(whereClause))
        {
            _testFilterBuilder.SelectWhere(BuildWhereClause());
        }

        return _testFilterBuilder.GetFilter();
    }
}
