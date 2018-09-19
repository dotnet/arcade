using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using HandlebarsDotNet;
using Microsoft.DotNet.SwaggerGenerator.Modeler;

namespace Microsoft.DotNet.SwaggerGenerator.Languages
{
    public class Templates
    {
        private Templates(
            Action<TextWriter, ServiceClientModel> serviceClient,
            Action<TextWriter, TypeModel> model,
            Action<TextWriter, MethodGroupModel> methodGroup)
        {
            ServiceClient = serviceClient;
            Model = model;
            MethodGroup = methodGroup;
        }

        public Action<TextWriter, ServiceClientModel> ServiceClient { get; }
        public Action<TextWriter, TypeModel> Model { get; }
        public Action<TextWriter, MethodGroupModel> MethodGroup { get; }

        public static Templates Load(string languageName, IHandlebars hb)
        {
            string templates = Path.Combine(
                Path.GetDirectoryName(typeof(Templates).Assembly.Location),
                "Languages",
                languageName);
            Action<TextWriter, object> serviceClient = LoadTemplateFile(hb, templates, "ServiceClient.hb");
            Action<TextWriter, object> model = LoadTemplateFile(hb, templates, "Model.hb");
            Action<TextWriter, object> methodGroup = LoadTemplateFile(hb, templates, "MethodGroup.hb");
            return new Templates(
                (writer, m) => serviceClient(writer, m),
                (writer, m) => model(writer, m),
                (writer, m) => methodGroup(writer, m));
        }

        private static Action<TextWriter, object> LoadTemplateFile(IHandlebars hb, string directory, string fileName)
        {
            var dir = new DirectoryInfo(directory);
            var file = new FileInfo(Path.Combine(dir.FullName, fileName));
            using (StreamReader reader = file.OpenText())
            {
                return hb.Compile(reader);
            }
        }
    }

    public abstract class Language
    {
        private static readonly Dictionary<string, Language> _languages;

        static Language()
        {
            _languages = new Dictionary<string, Language>(StringComparer.OrdinalIgnoreCase)
            {
                ["csharp"] = new CSharp(),
            };
        }

        public abstract string Extension { get; }

        public static Language Get(string name)
        {
            _languages.TryGetValue(name, out Language value);
            return value;
        }

        public abstract string ResolveReference(TypeReference reference);

        public abstract (string start, string end) NullCheck(TypeReference reference);

        public abstract (string start, string end) NotNullCheck(TypeReference reference);

        public abstract string GetHttpMethodReference(HttpMethod method);

        public abstract Templates GetTemplates(IHandlebars hb);


        private class CSharp : Language
        {
            public override string Extension => ".cs";

            public override string ResolveReference(TypeReference reference)
            {
                if (reference is TypeReference.ConstantTypeReference)
                {
                    return "string";
                }

                if (reference is TypeReference.TypeModelReference typeModelRef)
                {
                    return typeModelRef.Model.Name;
                }

                if (reference is TypeReference.ArrayTypeReference arrayTypeRef)
                {
                    return $"IImmutableList<{ResolveReference(arrayTypeRef.BaseType)}>";
                }

                if (reference is TypeReference.DictionaryTypeReference dictTypeRef)
                {
                    return $"IImmutableDictionary<string, {ResolveReference(dictTypeRef.ValueType)}>";
                }

                if (reference == TypeReference.Boolean)
                {
                    return "bool";
                }

                if (reference == TypeReference.Int32)
                {
                    return "int";
                }

                if (reference == TypeReference.Int64)
                {
                    return "long";
                }

                if (reference == TypeReference.Float)
                {
                    return "float";
                }

                if (reference == TypeReference.Double)
                {
                    return "double";
                }

                if (reference == TypeReference.String)
                {
                    return "string";
                }

                if (reference == TypeReference.Date)
                {
                    return "DateTimeOffset";
                }

                if (reference == TypeReference.DateTime)
                {
                    return "DateTimeOffset";
                }

                if (reference == TypeReference.Void)
                {
                    return "void";
                }

                if (reference == TypeReference.Any)
                {
                    return "Newtonsoft.Json.Linq.JToken";
                }

                if (reference == TypeReference.Byte)
                {
                    // TODO: implement this
                }

                throw new NotSupportedException(reference.ToString());
            }

            public override (string start, string end) NullCheck(TypeReference reference)
            {
                if (reference == TypeReference.String)
                {
                    return ("string.IsNullOrEmpty(", ")");
                }

                return ("", " == default");
            }

            public override (string start, string end) NotNullCheck(TypeReference reference)
            {
                if (reference == TypeReference.String)
                {
                    return ("!string.IsNullOrEmpty(", ")");
                }

                return ("", " != default");
            }

            public override string GetHttpMethodReference(HttpMethod method)
            {
                if (string.Equals(method.Method, "PATCH", StringComparison.OrdinalIgnoreCase))
                {
                    return "new HttpMethod(\"PATCH\")";
                }

                if (method == HttpMethod.Delete ||
                    method == HttpMethod.Get ||
                    method == HttpMethod.Head ||
                    method == HttpMethod.Options ||
                    method == HttpMethod.Post ||
                    method == HttpMethod.Put ||
                    method == HttpMethod.Trace)
                {
                    return $"HttpMethod.{Helpers.PascalCase(method.Method.AsSpan())}";
                }

                return $"new HttpMethod(\"{method.Method}\")";
            }

            public override Templates GetTemplates(IHandlebars hb)
            {
                return Templates.Load("csharp", hb);
            }
        }
    }
}
