using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using HandlebarsDotNet;
using Microsoft.DotNet.SwaggerGenerator.Modeler;

namespace Microsoft.DotNet.SwaggerGenerator.Languages
{
    partial class Language
    {
        private class CSharp : Language
        {
            [HelperMethod]
            public static string PascalCaseNs(string value)
            {
                string[] parts = value.Split('.');
                string ns = string.Join(".", parts.Select(p => Helpers.PascalCase(p.AsSpan())));
                return ns;
            }

            public override string Extension => ".cs";

            protected override string ResolveReference(TypeReference reference, object[] args)
            {
                if (reference is TypeReference.ConstantTypeReference)
                {
                    return "string";
                }

                if (reference is TypeReference.NullableTypeReference nullableType)
                {
                    return $"{ResolveReference(nullableType.BaseType, args)}?";
                }

                if (reference is TypeReference.TypeModelReference typeModelRef)
                {
                    return Helpers.PascalCase(typeModelRef.Model.Name.AsSpan());
                }

                if (reference is TypeReference.ArrayTypeReference arrayTypeRef)
                {
                    return $"IImmutableList<{ResolveReference(arrayTypeRef.BaseType, args)}>";
                }

                if (reference is TypeReference.DictionaryTypeReference dictTypeRef)
                {
                    return $"IImmutableDictionary<string, {ResolveReference(dictTypeRef.ValueType, args)}>";
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

            [BlockHelperMethod]
            public static void NullCheck(TextWriter output, object context, Action<TextWriter, object> template, TypeReference reference)
            {
                if (reference == TypeReference.String)
                {
                    output.Write("string.IsNullOrEmpty(");
                    template(output, context);
                    output.Write(")");
                }
                else
                {
                    template(output, context);
                    output.Write(" == default");
                }
            }

            [BlockHelperMethod]
            public static void NotNullCheck(TextWriter output, object context, Action<TextWriter, object> template, TypeReference reference)
            {
                if (reference == TypeReference.String)
                {
                    output.Write("!string.IsNullOrEmpty(");
                    template(output, context);
                    output.Write(")");
                }
                else
                {
                    template(output, context);
                    output.Write(" != default");
                }
            }

            [HelperMethod]
            public static string Method(HttpMethod method)
            {
                if (string.Equals(method.Method, "PATCH", StringComparison.OrdinalIgnoreCase))
                {
                    return "new HttpMethod(\"PATCH\")";
                }

                if (method == HttpMethod.Delete || method == HttpMethod.Get || method == HttpMethod.Head ||
                    method == HttpMethod.Options || method == HttpMethod.Post || method == HttpMethod.Put ||
                    method == HttpMethod.Trace)
                {
                    return $"HttpMethod.{Helpers.PascalCase(method.Method.ToLower().AsSpan())}";
                }

                return $"new HttpMethod(\"{method.Method}\")";
            }

            public override Templates GetTemplates(IHandlebars hb)
            {
                return Templates.Load("csharp", hb);
            }

            public override void GenerateCode(GenerateCodeContext context)
            {
                var model = context.ClientModel;
                context.WriteTemplate(context.Options.ClientName, context.Templates["ServiceClient"], model);

                foreach (TypeModel type in model.Types)
                {
                    context.WriteTemplate($"Models/{Helpers.PascalCase(type.Name.AsSpan())}", context.Templates["Model"], type);
                }

                foreach (MethodGroupModel group in model.MethodGroups)
                {
                    context.WriteTemplate(group.Name, context.Templates["MethodGroup"], group);
                }
            }
        }
    }
}
