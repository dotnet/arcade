// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Fx.ApiReviews.Differencing
{
    public sealed class DiffToken
    {
        public DiffToken(DiffStyle style, DiffTokenKind kind, string text)
        {
            Style = style;
            Text = text;
            Kind = kind;
        }

        public DiffStyle Style { get; private set; }
        public DiffTokenKind Kind { get; set; }
        public string Text { get; private set; }
        
        public override string ToString()
        {
            return Text;
        }
    }
}
