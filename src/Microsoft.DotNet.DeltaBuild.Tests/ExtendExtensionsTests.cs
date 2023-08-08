// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Xunit;

namespace Microsoft.DotNet.DeltaBuild.Tests;

public class ExtendExtensionsTests
{
    [Fact]
    public void Extend_DictionaryInitiallyEmpty_AddsFromItemWithToItems()
    {
        // Arrange
        var dict = new Dictionary<string, HashSet<int>>();
        const string fromItem = "test";
        var toItems = new List<int> {1, 2, 3};

        // Act
        dict.Extend(fromItem, toItems);

        // Assert
        Assert.True(dict.ContainsKey(fromItem));
        Assert.Equal(toItems, dict[fromItem]);
    }

    [Fact]
    public void Extend_DictionaryContainsFromItem_AddsNewToItems()
    {
        // Arrange
        var dict = new Dictionary<string, HashSet<int>>
        {
            ["test"] = new() {1, 2}
        };

        const string fromItem = "test";
        var toItems = new List<int> {2, 3, 4};  // Note: 2 is already present

        // Act
        dict.Extend(fromItem, toItems);

        // Assert
        Assert.Equal(new List<int> {1, 2, 3, 4}, dict[fromItem]);
    }

    [Fact]
    public void Extend_DictionaryContainsFromItemWithSameToItems_NoChanges()
    {
        // Arrange
        var dict = new Dictionary<string, HashSet<int>>
        {
            ["test"] = new() {1, 2, 3}
        };

        const string fromItem = "test";
        var toItems = new List<int> {1, 2, 3};

        // Act
        dict.Extend(fromItem, toItems);

        // Assert
        Assert.Equal(toItems, dict[fromItem]);
    }

    [Fact]
    public void Extend_FromItemIsNewButToItemsEmpty_AddsFromItemWithEmptyHashSet()
    {
        // Arrange
        var dict = new Dictionary<string, HashSet<int>>();
        const string fromItem = "test";

        // Act
        dict.Extend(fromItem, new List<int>());

        // Assert
        Assert.True(dict.ContainsKey(fromItem));
        Assert.Empty(dict[fromItem]);
    }

    [Fact]
    public void Extend_ToItemsHasDuplicates_DuplicatesIgnored()
    {
        // Arrange
        var dict = new Dictionary<string, HashSet<int>>();
        const string fromItem = "test";
        var toItems = new List<int> {1, 2, 2, 3, 3, 3};  // Note: duplicates present

        // Act
        dict.Extend(fromItem, toItems);

        // Assert
        Assert.True(dict.ContainsKey(fromItem));
        Assert.Equal(new List<int> {1, 2, 3}, dict[fromItem]);
    }

    [Fact]
    public void Extend_MultipleFromItemsSingleToItem_AddsFromItemsWithToItem()
    {
        // Arrange
        var dict = new Dictionary<string, HashSet<int>>();
        var fromItems = new List<string> {"test1", "test2", "test3"};
        const int toItem = 1;

        // Act
        dict.Extend(fromItems, toItem);

        // Assert
        foreach (string fromItem in fromItems)
        {
            Assert.True(dict.ContainsKey(fromItem));
            Assert.Equal(new HashSet<int> {toItem}, dict[fromItem]);
        }
    }

    [Fact]
    public void Extend_DictionaryContainsFromItems_AddsNewToItem()
    {
        // Arrange
        var dict = new Dictionary<string, HashSet<int>>
        {
            ["test1"] = new() {1},
            ["test2"] = new() {1},
            ["test3"] = new() {1}
        };

        var fromItems = new List<string> {"test1", "test2", "test3"};
        const int newItem = 2;

        // Act
        dict.Extend(fromItems, newItem);

        // Assert
        foreach (string fromItem in fromItems)
        {
            Assert.True(dict.ContainsKey(fromItem));
            Assert.Equal(new HashSet<int> {1, 2}, dict[fromItem]);
        }
    }

    [Fact]
    public void Extend_DictionaryContainsFromItemsWithSameToItem_NoChanges()
    {
        // Arrange
        var dict = new Dictionary<string, HashSet<int>>
        {
            ["test1"] = new() {1},
            ["test2"] = new() {1},
            ["test3"] = new() {1}
        };

        var fromItems = new List<string> {"test1", "test2", "test3"};
        const int existingItem = 1;

        // Act
        dict.Extend(fromItems, existingItem);

        // Assert
        foreach (string fromItem in fromItems)
        {
            Assert.True(dict.ContainsKey(fromItem));
            Assert.Equal(new HashSet<int> {1}, dict[fromItem]);
        }
    }
}
