using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Swashbuckle.AspNetCore.Swagger;

namespace Microsoft.DotNet.SwaggerGenerator
{
    public class SecuritySchemeConverter : JsonConverter
    {
        public override bool CanRead => true;
        public override bool CanWrite => false;

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override object ReadJson(
            JsonReader reader,
            Type objectType,
            object existingValue,
            JsonSerializer serializer)
        {
            JObject jsonObject = JObject.Load(reader);
            SecurityScheme scheme = null;
            string type = jsonObject["type"].ToString();
            switch (type)
            {
                case "apiKey":
                    scheme = new ApiKeyScheme();
                    break;
                case "oauth2":
                    scheme = new OAuth2Scheme();
                    break;
                case "basic":
                    scheme = new BasicAuthScheme();
                    break;
                default:
                    throw new ArgumentException($"Unexpected security scheme '{type}'");
            }

            serializer.Populate(jsonObject.CreateReader(), scheme);
            return scheme;
        }

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(SecurityScheme);
        }
    }
}
