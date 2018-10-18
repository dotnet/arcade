using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using HandlebarsDotNet;
using Microsoft.DotNet.SwaggerGenerator.Languages;
using Microsoft.DotNet.SwaggerGenerator.Modeler;

namespace Microsoft.DotNet.SwaggerGenerator
{
    public class ServiceClientCodeFactory
    {
        public List<CodeFile> GenerateCode(ServiceClientModel model, GeneratorOptions options)
        {
            Language language = Language.Get(options.LanguageName);

            IHandlebars hb = Handlebars.Create();

            RegisterHelpers(hb, language, options);

            Templates templates = language.GetTemplates(hb);

            var result = new List<CodeFile>();

            using (var writer = new StringWriter())
            {
                templates.ServiceClient(writer, model);
                result.Add(new CodeFile(options.ClientName + language.Extension, writer.ToString()));
            }

            foreach (TypeModel type in model.Types)
            {
                using (var writer = new StringWriter())
                {
                    templates.Model(writer, type);
                    result.Add(new CodeFile($"Models/{type.Name}{language.Extension}", writer.ToString()));
                }
            }

            foreach (MethodGroupModel group in model.MethodGroups)
            {
                using (var writer = new StringWriter())
                {
                    templates.MethodGroup(writer, group);
                    result.Add(new CodeFile(group.Name + language.Extension, writer.ToString()));
                }
            }

            return result;
        }

        private void RegisterHelpers(IHandlebars hb, Language language, GeneratorOptions options)
        {
            hb.RegisterHelper(
                "pascalCase",
                (writer, context, parameters) =>
                {
                    writer.Write(Helpers.PascalCase(((string) parameters[0]).AsSpan()));
                });
            hb.RegisterHelper(
                "camelCase",
                (writer, context, parameters) =>
                {
                    writer.Write(Helpers.CamelCase(((string) parameters[0]).AsSpan()));
                });
            hb.RegisterHelper(
                "pascalCaseNs",
                (writer, context, parameters) =>
                {
                    string[] nsParts = ((string) parameters[0]).Split('.');
                    string ns = string.Join(".", nsParts.Select(p => Helpers.PascalCase(p.AsSpan())));
                    writer.Write(ns);
                });
            hb.RegisterHelper("clientName", (writer, context, parameters) => { writer.Write(options.ClientName); });
            hb.RegisterHelper(
                "typeRef",
                (writer, context, parameters) =>
                {
                    var reference = (TypeReference) parameters[0];
                    writer.WriteSafeString(language.ResolveReference(reference));
                });
            hb.RegisterHelper(
                "method",
                (writer, context, parameters) =>
                {
                    var method = (HttpMethod) parameters[0];
                    writer.WriteSafeString(language.GetHttpMethodReference(method));
                });

            void WriteLanguageFormatString(TextWriter writer, HelperOptions opts, object context, string format)
            {
                var placeholderIdx = format.IndexOf(Language.FormatStringPlaceholder, StringComparison.Ordinal);
                if (placeholderIdx < 0)
                {
                    throw new InvalidOperationException("language format string missing placeholder");
                }
                writer.WriteSafeString(format.Substring(0, placeholderIdx));
                opts.Template(writer, context);
                writer.WriteSafeString(format.Substring(placeholderIdx + Language.FormatStringPlaceholder.Length));
            }

            hb.RegisterHelper(
                "nullCheck",
                (writer, opts, context, parameters) =>
                {
                    var typeReference = (TypeReference) parameters[0];
                    string format = language.NullCheckFormat(typeReference);
                    WriteLanguageFormatString(writer, opts, context, format);
                });

            hb.RegisterHelper(
                "notNullCheck",
                (writer, opts, context, parameters) =>
                {
                    var typeReference = (TypeReference) parameters[0];
                    string format = language.NotNullCheckFormat(typeReference);
                    WriteLanguageFormatString(writer, opts, context, format);
                });
        }
    }

    internal static class Encodings
    {
        public static readonly Encoding Utf8NoBom = new UTF8Encoding(false);
    }
}
