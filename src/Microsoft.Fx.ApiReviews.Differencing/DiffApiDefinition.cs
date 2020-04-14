// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.Cci;
using Microsoft.Cci.Differs;
using Microsoft.Fx.ApiReviews.Differencing.Helpers;
using Microsoft.Cci.Extensions;

namespace Microsoft.Fx.ApiReviews.Differencing
{
    public sealed class DiffApiDefinition
    {
        public DiffApiDefinition(IDefinition left, IDefinition right, DifferenceType difference, IList<DiffApiDefinition> children)
        {
            var representative = left ?? right;
            Name = NameHelper.GetName(representative);
            Definition = representative;
            Left = left;
            Right = right;
            Difference = difference;
            Children = new ReadOnlyCollection<DiffApiDefinition>(children);
        }

        public string Name { get; private set; }
        public IDefinition Definition { get; private set; }
        public IDefinition Left { get; private set; }
        public IDefinition Right { get; private set; }
        public int StartLine { get; internal set; }
        public int EndLine { get; internal set; }
        public DifferenceType Difference { get; private set; }
        public ReadOnlyCollection<DiffApiDefinition> Children { get; private set; }

        public override string ToString()
        {
            return Difference.ToString().Substring(0, 1) + " " + Definition.UniqueId();
        }
    }
}
