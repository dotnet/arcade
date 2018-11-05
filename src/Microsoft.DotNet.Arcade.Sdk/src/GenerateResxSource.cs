// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.DotNet.Arcade.Sdk
{
    public sealed class GenerateResxSource : Task
    {
        private const int maxDocCommentLength = 256;

        /// <summary>
        /// Language of source file to generate.  Supported languages: CSharp, VisualBasic
        /// </summary>
        [Required]
        public string Language { get; set; }

        /// <summary>
        /// Resources (resx) file.
        /// </summary>
        [Required]
        public string ResourceFile { get; set; }

        /// <summary>
        /// Name of the embedded resources to generate accessor class for.
        /// </summary>
        [Required]
        public string ResourceName { get; set; }

        /// <summary>
        /// Optionally, a namespace.type name for the generated Resources accessor class.  Defaults to ResourceName if unspecified.
        /// </summary>
        public string ResourceClassName { get; set; }

        /// <summary>
        /// If set to true the GetResourceString method is not included in the generated class and must be specified in a separate source file.
        /// </summary>
        public bool OmitGetResourceString { get; set; }

        /// <summary>
        /// If set to true, emits constant key strings instead of properties that retrieve values.
        /// </summary>
        /// <remarks>
        /// This support can be removed when the following issue has been resolved:
        ///     <see href="https://github.com/dotnet/wpf/issues/1">Use nameof(SRID.PropertyName) syntax instead of depending on GenerateResxSource.GenerateResourcesCodeAsConstants</see>
        /// </remarks>
        public bool AsConstants { get; set; }

        /// <summary>
        /// If set to true calls to GetResourceString receive a default resource string value.
        /// </summary>
        public bool IncludeDefaultValues { get; set; }

        [Required]
        public string OutputPath { get; set; }

        private enum Lang
        {
            CSharp,
            VisualBasic,
        }

        private bool IsLetterChar(UnicodeCategory cat)
        {
            // letter-character:
            //   A Unicode character of classes Lu, Ll, Lt, Lm, Lo, or Nl 
            //   A Unicode-escape-sequence representing a character of classes Lu, Ll, Lt, Lm, Lo, or Nl

            switch (cat)
            {
                case UnicodeCategory.UppercaseLetter:
                case UnicodeCategory.LowercaseLetter:
                case UnicodeCategory.TitlecaseLetter:
                case UnicodeCategory.ModifierLetter:
                case UnicodeCategory.OtherLetter:
                case UnicodeCategory.LetterNumber:
                    return true;
            }

            return false;
        }

        public override bool Execute()
        {
            string namespaceName;
            string className;

            if (string.IsNullOrEmpty(ResourceName))
            {
                Log.LogError("ResourceName not specified");
                return false;
            }

            string resourceAccessName = string.IsNullOrEmpty(ResourceClassName) ? ResourceName : ResourceClassName;
            SplitName(resourceAccessName, out namespaceName, out className);

            string docCommentStart;
            Lang language;
            switch (Language.ToUpperInvariant())
            {
                case "C#":
                    language = Lang.CSharp;
                    docCommentStart = "///";
                    break;

                case "VB":
                    language = Lang.VisualBasic;
                    docCommentStart = "'''";
                    break;

                default:
                    Log.LogError($"GenerateResxSource doesn't support language: '{Language}'");
                    return false;
            }

            string classIndent = (namespaceName == null ? "" : "    ");
            string memberIndent = classIndent + "    ";

            var strings = new StringBuilder();
            foreach (var node in XDocument.Load(ResourceFile).Descendants("data"))
            {
                string name = node.Attribute("name")?.Value;
                if (name == null)
                {
                    Log.LogError("Missing resource name");
                    return false;
                }

                string value = node.Elements("value").FirstOrDefault()?.Value.Trim();
                if (value == null)
                {
                    Log.LogError($"Missing resource value: '{name}'");
                    return false;
                }

                if (name == "")
                {
                    Log.LogError($"Empty resource name");
                    return false;
                }

                if (value.Length > maxDocCommentLength)
                {
                    value = value.Substring(0, maxDocCommentLength) + " ...";
                }

                string escapedTrimmedValue = new XElement("summary", value).ToString();

                foreach (var line in escapedTrimmedValue.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None))
                {
                    strings.Append($"{memberIndent}{docCommentStart} ");
                    strings.AppendLine(line);
                }

                string identifier = IsLetterChar(CharUnicodeInfo.GetUnicodeCategory(name[0])) ? name : "_" + name;

                string defaultValue = IncludeDefaultValues ? ", " + CreateStringLiteral(value, language) : string.Empty;

                switch (language)
                {
                    case Lang.CSharp:
                        if (AsConstants)
                        {
                            strings.AppendLine($"{memberIndent}internal const string {name} = nameof({name});");
                        }
                        else
                        {
                            strings.AppendLine($"{memberIndent}internal static string {identifier} => GetResourceString(\"{name}\"{defaultValue});");
                        }
                        break;

                    case Lang.VisualBasic:
                        if (AsConstants)
                        {
                            strings.AppendLine($"{memberIndent}Friend Shared ReadOnly Property {name} As String");
                        }
                        else
                        {
                            strings.AppendLine($"{memberIndent}Friend Shared ReadOnly Property {identifier} As String");
                        }
                        strings.AppendLine($"{memberIndent}  Get");
                        if (AsConstants)
                        {
                            strings.AppendLine($"{memberIndent}    Return \"{name}\"");
                        }
                        else
                        {
                            strings.AppendLine($"{memberIndent}    Return GetResourceString(\"{name}\"{defaultValue})");
                        }
                        strings.AppendLine($"{memberIndent}  End Get");
                        strings.AppendLine($"{memberIndent}End Property");
                        break;

                    default:
                        throw new InvalidOperationException();
                }
            }

            string getStringMethod;
            if (OmitGetResourceString)
            {
                getStringMethod = null;
            }
            else
            {
                switch (language)
                {
                    case Lang.CSharp:
                        getStringMethod = $@"{memberIndent}internal static global::System.Globalization.CultureInfo Culture {{ get; set; }}

{memberIndent}[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
{memberIndent}internal static string GetResourceString(string resourceKey, string defaultValue = null) =>  ResourceManager.GetString(resourceKey, Culture);";
                        break;

                    case Lang.VisualBasic:
                        getStringMethod = $@"{memberIndent}Friend Shared Property Culture As Global.System.Globalization.CultureInfo

{memberIndent}<Global.System.Runtime.CompilerServices.MethodImpl(Global.System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>
{memberIndent}Friend Shared Function GetResourceString(ByVal resourceKey As String, Optional ByVal defaultValue As String = Nothing) As String
{memberIndent}  Get
{memberIndent}    Return ResourceManager.GetString(resourceKey, Culture)
{memberIndent}  End Get
{memberIndent}End Function";
                        break;

                    default:
                        throw new InvalidOperationException();
                }
            }


            string namespaceStart, namespaceEnd;
            if (namespaceName == null)
            {
                namespaceStart = namespaceEnd = null;
            }
            else
            {
                switch (language)
                {
                    case Lang.CSharp:
                        namespaceStart = $@"namespace {namespaceName}{Environment.NewLine}{{";
                        namespaceEnd = "}";
                        break;

                    case Lang.VisualBasic:
                        namespaceStart = $"Namespace {namespaceName}";
                        namespaceEnd = "End Namespace";
                        break;

                    default:
                        throw new InvalidOperationException();
                }
            }

            string resourceTypeName;
            string resourceTypeDefinition;
            if (string.IsNullOrEmpty(ResourceClassName) || ResourceName == ResourceClassName)
            {
                // resource name is same as accessor, no need for a second type.
                resourceTypeName = className;
                resourceTypeDefinition = null;
            }
            else
            {
                // resource name differs from the access class, need a type for specifying the resources
                // this empty type must remain as it is required by the .NETNative toolchain for locating resources
                // once assemblies have been merged into the application
                resourceTypeName = ResourceName;

                string resourceNamespaceName;
                string resourceClassName;
                SplitName(resourceTypeName, out resourceNamespaceName, out resourceClassName);
                string resourceClassIndent = (resourceNamespaceName == null ? "" : "    ");

                switch (language)
                {
                    case Lang.CSharp:
                        resourceTypeDefinition = $"{resourceClassIndent}internal static class {resourceClassName} {{ }}";
                        if (resourceNamespaceName != null)
                        {
                            resourceTypeDefinition = $@"namespace {resourceNamespaceName}
{{
{resourceTypeDefinition}
}}";
                        }
                        break;

                    case Lang.VisualBasic:
                        resourceTypeDefinition = $@"{resourceClassIndent}Friend Class {resourceClassName}
{resourceClassIndent}End Class";
                        if (resourceNamespaceName != null)
                        {
                            resourceTypeDefinition = $@"Namespace {resourceNamespaceName}
{resourceTypeDefinition}
End Namespace";
                        }
                        break;

                    default:
                        throw new InvalidOperationException();
                }
            }

            // The ResourceManager property being initialized lazily is an important optimization that lets .NETNative 
            // completely remove the ResourceManager class if the disk space saving optimization to strip resources 
            // (/DisableExceptionMessages) is turned on in the compiler.
            string result;
            switch (language)
            {
                case Lang.CSharp:
                    result = $@"// <auto-generated>
using System.Reflection;

{resourceTypeDefinition}
{namespaceStart}
{classIndent}internal static partial class {className}
{classIndent}{{
{memberIndent}private static global::System.Resources.ResourceManager s_resourceManager;
{memberIndent}internal static global::System.Resources.ResourceManager ResourceManager => s_resourceManager ?? (s_resourceManager = new global::System.Resources.ResourceManager(typeof({resourceTypeName})));
{getStringMethod}
{strings}
{classIndent}}}
{namespaceEnd}
";
                    break;

                case Lang.VisualBasic:
                    result = $@"' <auto-generated>
Imports System.Reflection

{resourceTypeDefinition}
{namespaceStart}
{classIndent}Friend Partial Class {className}
{memberIndent}Private Sub New
{memberIndent}End Sub
{memberIndent}
{memberIndent}Private Shared s_resourceManager As Global.System.Resources.ResourceManager
{memberIndent}Friend Shared ReadOnly Property ResourceManager As Global.System.Resources.ResourceManager
{memberIndent}    Get
{memberIndent}        If s_resourceManager Is Nothing Then
{memberIndent}            s_resourceManager = New Global.System.Resources.ResourceManager(GetType({resourceTypeName}))
{memberIndent}        End If
{memberIndent}        Return s_resourceManager
{memberIndent}    End Get
{memberIndent}End Property
{getStringMethod}
{strings}
{classIndent}End Class
{namespaceEnd}
";
                    break;

                default:
                    throw new InvalidOperationException();
            }

            File.WriteAllText(OutputPath, result);
            return true;
        }

        private static string CreateStringLiteral(string original, Lang lang)
        {
            StringBuilder stringLiteral = new StringBuilder(original.Length + 3);
            if (lang == Lang.CSharp)
            {
                stringLiteral.Append('@');
            }
            stringLiteral.Append('\"');
            for (var i = 0; i < original.Length; i++)
            {
                // duplicate '"' for VB and C#
                if (original[i] == '\"')
                {
                    stringLiteral.Append("\"");
                }
                stringLiteral.Append(original[i]);
            }
            stringLiteral.Append('\"');

            return stringLiteral.ToString();
        }

        private static void SplitName(string fullName, out string namespaceName, out string className)
        {
            int lastDot = fullName.LastIndexOf('.');
            if (lastDot == -1)
            {
                namespaceName = null;
                className = fullName;
            }
            else
            {
                namespaceName = fullName.Substring(0, lastDot);
                className = fullName.Substring(lastDot + 1);
            }
        }
    }
}
