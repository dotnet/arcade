﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Collections.Immutable;

namespace Microsoft.DotNet.SwaggerGenerator.Modeler
{
    public class EnumTypeModel : TypeModel
    {
        public EnumTypeModel(string name, string ns, IEnumerable<string> values)
        {
            Name = name;
            Namespace = ns;
            Values = values.ToImmutableList();
        }

        public override string Name { get; }
        public override string Namespace { get; }
        public override bool IsEnum => true;
        public IImmutableList<string> Values { get; }
    }
}
