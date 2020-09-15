// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ComponentModel;

namespace AttributeDifference
{
    [Designer("Foo")]
    [DisplayName("Attribute difference class1")]
    [Foo]
    public class AttributeDifferenceClass1
    {
    }

    internal class FooAttribute : Attribute { }
}
