using System;
using System.IO;
using System.Net.Http;
using HandlebarsDotNet;
using Microsoft.DotNet.SwaggerGenerator.Modeler;

namespace Microsoft.DotNet.SwaggerGenerator.Languages
{
    partial class Language
    {
        private class TypescriptFetch : Language
        {
            public override string Extension => ".ts";

            protected override string ResolveReference(TypeReference reference, object[] args)
            {
                string classPrefix = null;
                if (args.Length == 1)
                {
                    classPrefix = (string) args[0];
                }

                if (reference is TypeReference.ConstantTypeReference)
                {
                    return "string";
                }

                if (reference is TypeReference.TypeModelReference typeModelRef)
                {
                    return classPrefix + typeModelRef.Model.Name;
                }

                if (reference is TypeReference.ArrayTypeReference arrayTypeRef)
                {
                    return $"{ResolveReference(arrayTypeRef.BaseType, args)}[]";
                }

                if (reference is TypeReference.DictionaryTypeReference dictTypeRef)
                {
                    return $"Record<string, {ResolveReference(dictTypeRef.ValueType, args)}>";
                }

                if (reference == TypeReference.Boolean)
                {
                    return "boolean";
                }

                if (reference == TypeReference.Int32 ||
                    reference == TypeReference.Int64 ||
                    reference == TypeReference.Float ||
                    reference == TypeReference.Double)
                {
                    return "number";
                }

                if (reference == TypeReference.String)
                {
                    return "string";
                }

                if (reference == TypeReference.Date)
                {
                    return "Moment";
                }

                if (reference == TypeReference.DateTime)
                {
                    return "Moment";
                }

                if (reference == TypeReference.Void)
                {
                    return "void";
                }

                if (reference == TypeReference.Any)
                {
                    return "any";
                }

                if (reference == TypeReference.Byte)
                {
                    return "Buffer";
                }

                throw new NotSupportedException(reference.ToString());
            }

            public override Templates GetTemplates(IHandlebars hb)
            {
                return Templates.Load("typescript-fetch", hb);
            }

            public override void GenerateCode(GenerateCodeContext context)
            {
                var model = context.ClientModel;

                context.WriteTemplate(Helpers.KebabCase(context.Options.ClientName.AsSpan()), context.Templates["ServiceClient"], model);

                context.WriteTemplate("service-client", context.Templates["ServiceClientInterface"], null);

                context.WriteTemplate("models", context.Templates["Models"], model);

                context.WriteTemplate("method-groups", context.Templates["MethodGroups"], model);
            }

            [BlockHelperMethod]
            public void Deserialize(TextWriter output, object context, Action<TextWriter, object> template, TypeReference type, string modelTypePrefix)
            {
                if (type == TypeReference.Byte)
                {
                    output.Write("Buffer.from(");
                    template(output, context);
                    output.Write(", 'base64')");
                }
                else if (type == TypeReference.Date ||
                         type == TypeReference.DateTime)
                {
                    output.Write("moment(");
                    template(output, context);
                    output.Write(")");
                }
                else if
                    (type is TypeReference.TypeModelReference typeModel &&
                     !typeModel.Model.IsEnum)
                {
                    output.Write(modelTypePrefix);
                    output.Write(Helpers.PascalCase(typeModel.DisplayString.AsSpan()));
                    output.Write(".deserialize(");
                    template(output, context);
                    output.Write(")");
                }
                else
                {
                    template(output, context);
                }
            }

            [BlockHelperMethod]
            public void Serialize(TextWriter output, object context, Action<TextWriter, object> template, TypeReference type, string modelTypePrefix)
            {
                if (type == TypeReference.Byte)
                {
                    template(output, context);
                    output.WriteSafeString(" && ");;
                    template(output, context);
                    output.WriteSafeString(".toString('base64')");
                }
                else if (type == TypeReference.Date)
                {
                    template(output, context);
                    output.WriteSafeString(" && ");
                    template(output, context);
                    output.WriteSafeString(".format('YYYY-MM-DD')");
                }
                else if (type == TypeReference.DateTime)
                {
                    template(output, context);
                    output.WriteSafeString(" && ");
                    template(output, context);
                    output.WriteSafeString(".format('YYYY-MM-DDTHH:mm:ssZ')");
                }
                else if
                    (type is TypeReference.TypeModelReference typeModel &&
                     !typeModel.Model.IsEnum)
                {
                    output.Write(modelTypePrefix);
                    output.WriteSafeString(Helpers.PascalCase(typeModel.DisplayString.AsSpan()));
                    output.WriteSafeString(".serialize(");
                    template(output, context);
                    output.WriteSafeString(")");
                }
                else
                {
                    template(output, context);
                }
            }
        }
    }
}
