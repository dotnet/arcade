using System;
using System.Text;
using Microsoft.DotNet.SwaggerGenerator.Modeler;
using Newtonsoft.Json;
using Swashbuckle.AspNetCore.Swagger;

namespace Microsoft.DotNet.SwaggerGenerator
{
    public static class Helpers
    {
        private static ReadOnlySpan<char> GetNextWord(ReadOnlySpan<char> value, ref int pos)
        {
            int? wordStart = null;
            for (int idx = pos; idx < value.Length; idx++)
            {
                if (wordStart.HasValue)
                {
                    if (!char.IsLetterOrDigit(value[idx]) || char.IsUpper(value[idx]))
                    {
                        pos = idx;
                        return value.Slice(wordStart.Value, idx - wordStart.Value);
                    }
                }
                else
                {
                    if (char.IsLetterOrDigit(value[idx]))
                    {
                        wordStart = idx;
                    }
                }
            }

            pos = value.Length;

            if (wordStart.HasValue)
            {
                return value.Slice(wordStart.Value);
            }

            return value.Slice(0, 0);
        }

        public static string PascalCase(ReadOnlySpan<char> value)
        {
            var builder = new StringBuilder();
            ReadOnlySpan<char> word;
            var pos = 0;
            while ((word = GetNextWord(value, ref pos)).Length != 0)
            {
                for (var i = 0; i < word.Length; i++)
                {
                    char c;
                    if (i == 0)
                    {
                        c = char.ToUpperInvariant(word[i]);
                    }
                    else
                    {
                        c = char.ToLowerInvariant(word[i]);
                    }

                    builder.Append(c);
                }
            }

            return builder.ToString();
        }

        public static string CamelCase(ReadOnlySpan<char> value)
        {
            var builder = new StringBuilder();
            ReadOnlySpan<char> word;
            var pos = 0;
            var first = true;
            while ((word = GetNextWord(value, ref pos)).Length != 0)
            {
                for (var i = 0; i < word.Length; i++)
                {
                    char c;
                    if (i == 0 && !first)
                    {
                        c = char.ToUpperInvariant(word[i]);
                    }
                    else
                    {
                        c = char.ToLowerInvariant(word[i]);
                    }

                    builder.Append(c);
                }

                first = false;
            }

            return builder.ToString();
        }
    }

    public static class SwaggerSerializer
    {
        public static SwaggerDocument Deserialize(JsonReader reader)
        {
            var serializer = new JsonSerializer
            {
                ContractResolver = new SwaggerContractResolver(new JsonSerializerSettings()),
                Converters = {new ParameterConverter(), new SecuritySchemeConverter()},
                MetadataPropertyHandling = MetadataPropertyHandling.Ignore
            };
            return serializer.Deserialize<SwaggerDocument>(reader);
        }
    }
}
