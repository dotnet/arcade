using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using HandlebarsDotNet;
using SwaggerGenerator.csharp;
using SwaggerGenerator.Languages;
using SwaggerGenerator.Modeler;

namespace SwaggerGenerator
{
    public class ServiceClientCodeFactory
    {
        public List<CodeFile> GenerateCode(ServiceClientModel model, GeneratorOptions options)
        {
            var language = Language.Get(options.LanguageName);

            var hb = Handlebars.Create();

            RegisterHelpers(hb, language, options);

            var templates = language.GetTemplates(hb);

            var result = new List<CodeFile>();

            using (var writer = new StringWriter())
            {
                templates.ServiceClient(writer, model);
                result.Add(new CodeFile(options.ClientName + language.Extension, writer.ToString()));
            }

            foreach (var type in model.Types)
            {
                using (var writer = new StringWriter())
                {
                    templates.Model(writer, type);
                    result.Add(new CodeFile($"Models/{type.Name}{language.Extension}", writer.ToString()));
                }
            }

            foreach (var group in model.MethodGroups)
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
            hb.RegisterHelper("pascalCase",
                (writer, context, parameters) =>
                {
                    writer.Write(Helpers.PascalCase(((string)parameters[0]).AsSpan()));
                });
            hb.RegisterHelper("camelCase",
                (writer, context, parameters) =>
                {
                    writer.Write(Helpers.CamelCase(((string)parameters[0]).AsSpan()));
                });
            hb.RegisterHelper("pascalCaseNs",
                (writer, context, parameters) =>
                {
                    var nsParts = ((string) parameters[0]).Split('.');
                    var ns = string.Join(".", nsParts.Select(p => Helpers.PascalCase(p.AsSpan())));
                    writer.Write(ns);
                });
            hb.RegisterHelper("clientName",
                (writer, context, parameters) =>
                {
                    writer.Write(options.ClientName);
                });
            hb.RegisterHelper("typeRef",
                (writer, context, parameters) =>
                {
                    var reference = (TypeReference) parameters[0];
                    writer.WriteSafeString(language.ResolveReference(reference));
                });
            hb.RegisterHelper("method",
                (writer, context, parameters) =>
                {
                    var method = (HttpMethod)parameters[0];
                    writer.WriteSafeString(language.HttpMethod(method));
                });

            hb.RegisterHelper("nullCheck",
                (writer, opts, context, parameters) =>
                {
                    var typeReference = (TypeReference) parameters[0];
                    var (start, end) = language.NullCheck(typeReference);
                    writer.WriteSafeString(start);
                    opts.Template(writer, context);
                    writer.WriteSafeString(end);
                });

            hb.RegisterHelper("notNullCheck",
                (writer, opts, context, parameters) =>
                {
                    var typeReference = (TypeReference) parameters[0];
                    var (start, end) = language.NotNullCheck(typeReference);
                    writer.WriteSafeString(start);
                    opts.Template(writer, context);
                    writer.WriteSafeString(end);
                });
        }
    }

    internal static class Encodings
    {
        public static readonly Encoding Utf8NoBom = new UTF8Encoding(false);
    }
}