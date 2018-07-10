// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace RoslynTools
{
    public sealed class GenerateResxSource : Task
    {
        private const int maxDocCommentLength = 256;

        [Required]
        public string Language { get; set; }

        [Required]
        public string ResourceFile { get; set; }

        [Required]
        public string ResourceName { get; set; }

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

            string[] nameParts = ResourceName.Split('.');
            if (nameParts.Length == 1)
            {
                namespaceName = null;
                className = nameParts[0];
            }
            else
            {
                namespaceName = string.Join(".", nameParts, 0, nameParts.Length - 1);
                className = nameParts.Last();
            }

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

                switch (language)
                {
                    case Lang.CSharp:
                        strings.AppendLine($"{memberIndent}internal static string {identifier} => ResourceManager.GetString(\"{name}\", Culture);");
                        break;

                    case Lang.VisualBasic:
                        strings.AppendLine($"{memberIndent}Friend Shared ReadOnly Property {identifier} As String");
                        strings.AppendLine($"{memberIndent}  Get");
                        strings.AppendLine($"{memberIndent}    Return ResourceManager.GetString(\"{name}\", Culture)");
                        strings.AppendLine($"{memberIndent}  End Get");
                        strings.AppendLine($"{memberIndent}End Property");
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

            string result;
            switch (language)
            {
                case Lang.CSharp:
                    result = $@"// <auto-generated>
using System.Reflection;

{namespaceStart}
{classIndent}internal static class {className}
{classIndent}{{
{memberIndent}internal static global::System.Globalization.CultureInfo Culture {{ get; set; }}
{memberIndent}internal static global::System.Resources.ResourceManager ResourceManager {{ get; }} = new global::System.Resources.ResourceManager(""{ResourceName}"", typeof({className}).GetTypeInfo().Assembly);

{strings}
{classIndent}}}
{namespaceEnd}
";
                    break;

                case Lang.VisualBasic:
                    result = $@"' <auto-generated>
Imports System.Reflection

{namespaceStart}
{classIndent}Friend Class {className}
{memberIndent}Private Sub New
{memberIndent}End Sub
{memberIndent}
{memberIndent}Friend Shared Property Culture As Global.System.Globalization.CultureInfo
{memberIndent}Friend Shared ReadOnly Property ResourceManager As New Global.System.Resources.ResourceManager(""{ResourceName}"", GetType({className}).GetTypeInfo().Assembly)

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
    }
}
