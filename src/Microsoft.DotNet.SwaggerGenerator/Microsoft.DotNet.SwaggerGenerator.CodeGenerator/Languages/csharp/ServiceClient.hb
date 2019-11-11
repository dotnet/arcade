using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Security.Authentication;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Rest;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace {{pascalCaseNs Namespace}}
{
    public partial interface I{{pascalCase Name}} : IDisposable
    {
        Uri BaseUri { get; set; }

        {{#each MethodGroups}}
        I{{pascalCase Name}} {{pascalCase Name}} { get; }
        {{/each}}
    }

    public partial class {{pascalCase Name}} : ServiceClient<{{pascalCase Name}}>, I{{pascalCase Name}}
    {
        /// <summary>
        ///   The base URI of the service.
        /// </summary>
        public Uri BaseUri { get; set; }

        /// <summary>
        ///   Credentials to authenticate requests.
        /// </summary>
        public ServiceClientCredentials Credentials { get; set; }

        public JsonSerializerSettings SerializerSettings { get; }

        {{#each MethodGroups}}
        public I{{pascalCase Name}} {{pascalCase Name}} { get; }

        {{/each}}

        public {{pascalCase Name}}(params DelegatingHandler[] handlers)
            :this(null, null, handlers)
        {
        }

        public {{pascalCase Name}}(Uri baseUri, params DelegatingHandler[] handlers)
            :this(baseUri, null, handlers)
        {
        }

        public {{pascalCase Name}}(ServiceClientCredentials credentials, params DelegatingHandler[] handlers)
            :this(null, credentials, handlers)
        {
        }

        public {{pascalCase Name}}(Uri baseUri, ServiceClientCredentials credentials, params DelegatingHandler[] handlers)
            :base(handlers)
        {
            HttpClientHandler.SslProtocols = SslProtocols.Tls12;
            BaseUri = baseUri ?? new Uri("{{BaseUrl}}/");
            Credentials = credentials;
            {{#each MethodGroups}}
            {{pascalCase Name}} = new {{pascalCase Name}}(this);
            {{/each}}
            SerializerSettings = new JsonSerializerSettings
            {
                Converters =
                {
                    new StringEnumConverter()
                },
                NullValueHandling = NullValueHandling.Ignore,
            };

            Init();
        }

        /// <summary>
        ///    Optional initialization defined outside of auto-gen code
        /// </summary>
        partial void Init();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void OnFailedRequest(RestApiException ex)
        {
            HandleFailedRequest(ex);
        }

        partial void HandleFailedRequest(RestApiException ex);


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string Serialize(string value)
        {
            return value;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string Serialize(bool value)
        {
            return value ? "true" : "false";
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string Serialize(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string Serialize(long value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string Serialize(float value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string Serialize(double value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string Serialize(Guid value)
        {
            return value.ToString("D", CultureInfo.InvariantCulture);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string Serialize<T>(T value)
        {
            string result = JsonConvert.SerializeObject(value, SerializerSettings);

            if (value is Enum)
            {
                return result.Substring(1, result.Length-2);
            }

            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Deserialize<T>(string value)
        {
            return JsonConvert.DeserializeObject<T>(value, SerializerSettings);
        }

        public virtual Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return HttpClient.SendAsync(request, cancellationToken);
        }
    }

    public class AllPropertiesContractResolver : DefaultContractResolver
    {
        protected override JsonProperty CreateProperty(
            MemberInfo member,
            MemberSerialization memberSerialization)
        {
            var prop = base.CreateProperty(member, memberSerialization);

            if (!prop.Writable)
            {
                var property = member as PropertyInfo;
                if (property != null)
                {
                    var hasPrivateSetter = property.GetSetMethod(true) != null;
                    prop.Writable = hasPrivateSetter;
                }
            }

            return prop;
        }
    }

    [Serializable]
    public partial class RestApiException : Exception
    {
        private static readonly JsonSerializerSettings SerializerSettings = new JsonSerializerSettings
        {
            ContractResolver = new AllPropertiesContractResolver(),
        };

        private static string FormatMessage(HttpResponseMessageWrapper response)
        {
            var result = $"The response contained an invalid status code {(int)response.StatusCode} {response.ReasonPhrase}";
            if (!string.IsNullOrEmpty(response.Content))
            {
                result += "\n\nBody: ";
                result += response.Content.Length < 300 ? response.Content : response.Content.Substring(0, 300);
            }
            return result;
        }

        public HttpRequestMessageWrapper Request { get; }

        public HttpResponseMessageWrapper Response { get; }

        public RestApiException(HttpRequestMessageWrapper request, HttpResponseMessageWrapper response)
           :this(FormatMessage(response), request, response)
        {
        }

        public RestApiException(string message, HttpRequestMessageWrapper request, HttpResponseMessageWrapper response)
           :base(message)
        {
            Request = request;
            Response = response;
        }

        protected RestApiException(SerializationInfo info, StreamingContext context)
            :base(info, context)
        {
            var requestString = info.GetString("Request");
            var responseString = info.GetString("Response");
            Request = JsonConvert.DeserializeObject<HttpRequestMessageWrapper>(requestString, SerializerSettings);
            Response = JsonConvert.DeserializeObject<HttpResponseMessageWrapper>(responseString, SerializerSettings);
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null)
            {
                throw new ArgumentNullException(nameof(info));
            }

            var requestString = JsonConvert.SerializeObject(Request, SerializerSettings);
            var responseString = JsonConvert.SerializeObject(Response, SerializerSettings);

            info.AddValue("Request", requestString);
            info.AddValue("Response", responseString);
            base.GetObjectData(info, context);
        }
    }

    [Serializable]
    public partial class RestApiException<T> : RestApiException
    {
        public T Body { get; }

        public RestApiException(HttpRequestMessageWrapper request, HttpResponseMessageWrapper response, T body)
           :base(request, response)
        {
            Body = body;
        }

        public RestApiException(string message, HttpRequestMessageWrapper request, HttpResponseMessageWrapper response, T body)
           :base(message, request, response)
        {
            Body = body;
        }

        protected RestApiException(SerializationInfo info, StreamingContext context)
            :base(info, context)
        {
            Body = JsonConvert.DeserializeObject<T>(info.GetString("Body"));
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null)
            {
                throw new ArgumentNullException(nameof(info));
            }

            info.AddValue("Body", JsonConvert.SerializeObject(Body));
            base.GetObjectData(info, context);
        }
    }

    public partial class QueryBuilder : List<KeyValuePair<string, string>>
    {
        public QueryBuilder()
        {
        }

        public QueryBuilder(IEnumerable<KeyValuePair<string, string>> parameters)
            :base(parameters)
        {
        }

        public void Add(string key, IEnumerable<string> values)
        {
            foreach (string str in values)
                Add(new KeyValuePair<string, string>(key, str));
        }

        public void Add(string key, string value)
        {
            Add(new KeyValuePair<string, string>(key, value));
        }

        public override string ToString()
        {
          var builder = new StringBuilder();
          for (int index = 0; index < Count; ++index)
          {
            KeyValuePair<string, string> keyValuePair = this[index];
            if (index != 0)
            {
                builder.Append("&");
            }
            builder.Append(UrlEncoder.Default.Encode(keyValuePair.Key));
            builder.Append("=");
            builder.Append(UrlEncoder.Default.Encode(keyValuePair.Value));
          }
          return builder.ToString();
        }
    }

    
    public class ResponseStream : Stream
    {
        private readonly Stream _inner;
        private readonly HttpOperationResponse _response;

        public ResponseStream(Stream inner, HttpOperationResponse response)
        {
            _inner = inner;
            _response = response;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                _inner.Dispose();
                _response.Dispose();
            }
        }

        #region Forwarding Members

        public override void Flush()
        {
            _inner.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return _inner.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return _inner.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            _inner.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _inner.Write(buffer, offset, count);
        }

        public override string ToString()
        {
            return _inner.ToString();
        }

        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            return _inner.BeginRead(buffer, offset, count, callback, state);
        }

        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            return _inner.BeginWrite(buffer, offset, count, callback, state);
        }

        public override void Close()
        {
            _inner.Close();
        }

        public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
        {
            return _inner.CopyToAsync(destination, bufferSize, cancellationToken);
        }

        public override int EndRead(IAsyncResult asyncResult)
        {
            return _inner.EndRead(asyncResult);
        }

        public override void EndWrite(IAsyncResult asyncResult)
        {
            _inner.EndWrite(asyncResult);
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            return _inner.FlushAsync(cancellationToken);
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return _inner.ReadAsync(buffer, offset, count, cancellationToken);
        }

        public override int ReadByte()
        {
            return _inner.ReadByte();
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return _inner.WriteAsync(buffer, offset, count, cancellationToken);
        }

        public override void WriteByte(byte value)
        {
            _inner.WriteByte(value);
        }

        public override bool CanRead => _inner.CanRead;

        public override bool CanSeek => _inner.CanSeek;

        public override bool CanWrite => _inner.CanWrite;

        public override long Length => _inner.Length;

        public override long Position
        {
            get => _inner.Position;
            set => _inner.Position = value;
        }

        public override bool CanTimeout => _inner.CanTimeout;

        public override int ReadTimeout { get => _inner.ReadTimeout; set => _inner.ReadTimeout = value; }
        public override int WriteTimeout { get => _inner.WriteTimeout; set => _inner.WriteTimeout = value; }

        #endregion
    }
}
