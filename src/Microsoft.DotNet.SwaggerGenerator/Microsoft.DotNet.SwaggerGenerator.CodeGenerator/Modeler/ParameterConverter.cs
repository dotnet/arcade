using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Swashbuckle.AspNetCore.Swagger;

namespace Microsoft.DotNet.SwaggerGenerator.Modeler
{
    public class ParameterConverter : JsonConverter
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
            IParameter parameter = null;
            if (jsonObject["in"].ToString() == "body")
            {
                parameter = new BodyParameter();
            }
            else
            {
                parameter = new NonBodyParameter();
            }

            serializer.Populate(jsonObject.CreateReader(), parameter);
            return parameter;
        }

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(IParameter);
        }
    }
}
