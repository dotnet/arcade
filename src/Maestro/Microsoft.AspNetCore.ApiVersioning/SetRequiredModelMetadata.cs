// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;

namespace Microsoft.AspNetCore.ApiVersioning
{
    /// <summary>
    ///     Wrapper class that wraps a ModelMedatata object and forces IsRequired to true
    ///     only needed because ModelMetadata is very restrictive
    /// </summary>
    public class SetRequiredModelMetadata : ModelMetadata
    {
        private readonly ModelMetadata _inner;

        public SetRequiredModelMetadata(ModelMetadata inner) : base(ModelMetadataIdentity.ForType(inner.ModelType))
        {
            _inner = inner;
        }

        public override bool IsRequired => true;
        public override IReadOnlyDictionary<object, object> AdditionalValues => _inner.AdditionalValues;
        public override ModelPropertyCollection Properties => _inner.Properties;
        public override string BinderModelName => _inner.BinderModelName;
        public override Type BinderType => _inner.BinderType;
        public override BindingSource BindingSource => _inner.BindingSource;
        public override bool ConvertEmptyStringToNull => _inner.ConvertEmptyStringToNull;
        public override string DataTypeName => _inner.DataTypeName;
        public override string Description => _inner.Description;
        public override string DisplayFormatString => _inner.DisplayFormatString;
        public override string DisplayName => _inner.DisplayName;
        public override string EditFormatString => _inner.EditFormatString;
        public override ModelMetadata ElementMetadata => _inner.ElementMetadata;

        public override IEnumerable<KeyValuePair<EnumGroupAndName, string>> EnumGroupedDisplayNamesAndValues =>
            _inner.EnumGroupedDisplayNamesAndValues;

        public override IReadOnlyDictionary<string, string> EnumNamesAndValues => _inner.EnumNamesAndValues;
        public override bool HasNonDefaultEditFormat => _inner.HasNonDefaultEditFormat;
        public override bool HtmlEncode => _inner.HtmlEncode;
        public override bool HideSurroundingHtml => _inner.HideSurroundingHtml;
        public override bool IsBindingAllowed => _inner.IsBindingAllowed;
        public override bool IsBindingRequired => _inner.IsBindingRequired;
        public override bool IsEnum => _inner.IsEnum;
        public override bool IsFlagsEnum => _inner.IsFlagsEnum;
        public override bool IsReadOnly => _inner.IsReadOnly;
        public override ModelBindingMessageProvider ModelBindingMessageProvider => _inner.ModelBindingMessageProvider;
        public override int Order => _inner.Order;
        public override string Placeholder => _inner.Placeholder;
        public override string NullDisplayText => _inner.NullDisplayText;
        public override IPropertyFilterProvider PropertyFilterProvider => _inner.PropertyFilterProvider;
        public override bool ShowForDisplay => _inner.ShowForDisplay;
        public override bool ShowForEdit => _inner.ShowForEdit;
        public override string SimpleDisplayProperty => _inner.SimpleDisplayProperty;
        public override string TemplateHint => _inner.TemplateHint;
        public override bool ValidateChildren => _inner.ValidateChildren;
        public override IReadOnlyList<object> ValidatorMetadata => _inner.ValidatorMetadata;
        public override Func<object, object> PropertyGetter => _inner.PropertyGetter;
        public override Action<object, object> PropertySetter => _inner.PropertySetter;
    }
}
