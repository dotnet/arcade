using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Rest;
using {{pascalCaseNs Namespace}}.Models;

namespace {{pascalCaseNs Namespace}}
{
    public partial interface I{{pascalCase Name}}
    {
        {{#each Methods}}
        {{#if ResponseIsVoid}}Task{{else}}Task<
        {{~#if Paginated~}}
        PagedResponse<{{typeRef ResponseType.BaseType}}>
        {{~else~}}
        {{~typeRef ResponseType~}}
        {{~/if~}}>{{/if}} {{Name}}Async(
            {{#each FormalParameters}}
            {{typeRef Type}}{{#if (and (not Required) (not (IsNullable Type)))}}?{{/if}} {{camelCase Name}}{{#unless Required}} = default{{/unless}},
            {{/each}}
            CancellationToken cancellationToken = default
        );

        {{/each}}
    }

    internal partial class {{pascalCase Name}} : IServiceOperations<{{clientName null}}>, I{{pascalCase Name}}
    {
        public {{pascalCase Name}}({{clientName null}} client)
        {
            Client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public {{clientName null}} Client { get; }

        partial void HandleFailedRequest(RestApiException ex);
        {{#each Methods}}

        partial void HandleFailed{{Name}}Request(RestApiException ex);

        public async {{#if ResponseIsVoid}}Task{{else}}Task<
        {{~#if Paginated~}}
        PagedResponse<{{typeRef ResponseType.BaseType}}>
        {{~else~}}
        {{~typeRef ResponseType~}}
        {{~/if~}}>{{/if}} {{Name}}Async(
            {{#each FormalParameters}}
            {{typeRef Type}}{{#if (and (not Required) (not (IsNullable Type)))}}?{{/if}} {{camelCase Name}}{{#unless Required}} = default{{/unless}},
            {{/each}}
            CancellationToken cancellationToken = default
        )
        {
            {{#if ResponseIsVoid}}
            using (await {{Name}}InternalAsync(
                {{#each FormalParameters}}
                {{camelCase Name}},
                {{/each}}
                cancellationToken
            ).ConfigureAwait(false))
            {
                return;
            }
            {{else}}
            {{#if ResponseIsFile}}
            var _res = await {{Name}}InternalAsync(
                {{#each FormalParameters}}
                {{camelCase Name}},
                {{/each}}
                cancellationToken
            ).ConfigureAwait(false);
            return new ResponseStream(_res.Body, _res);
            {{else}}
            using (var _res = await {{Name}}InternalAsync(
                {{#each FormalParameters}}
                {{camelCase Name}},
                {{/each}}
                cancellationToken
            ).ConfigureAwait(false))
            {
                {{#if Paginated}}
                return new PagedResponse<{{typeRef ResponseType.BaseType}}>(Client, On{{Name}}Failed, _res);
                {{else}}
                return _res.Body;
                {{/if}}
            }
            {{/if}}
            {{/if}}
        }

        internal async Task On{{Name}}Failed(HttpRequestMessage req, HttpResponseMessage res)
        {
            var content = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
            var ex = new RestApiException{{#if ErrorType}}<{{typeRef ErrorType}}>{{/if}}(
                new HttpRequestMessageWrapper(req, {{#if BodyParameter}}content{{else}}null{{/if}}),
                new HttpResponseMessageWrapper(res, content)
                {{~#if ErrorType}},
                Client.Deserialize<{{typeRef ErrorType}}>(content)
                {{/if~}});
            HandleFailed{{Name}}Request(ex);
            HandleFailedRequest(ex);
            Client.OnFailedRequest(ex);
            throw ex;
        }

        internal async Task<HttpOperationResponse{{#unless ResponseIsVoid}}<{{typeRef ResponseType}}>{{/unless}}> {{Name}}InternalAsync(
            {{#each FormalParameters}}
            {{typeRef Type}}{{#if (and (not Required) (not (IsNullable Type)))}}?{{/if}} {{camelCase Name}}{{#unless Required}} = default{{/unless}},
            {{/each}}
            CancellationToken cancellationToken = default
        )
        {
            {{#each NonConstantParameters}}
            {{#if Required}}
            if ({{#nullCheck Type Required}}{{camelCase Name}}{{/nullCheck}})
            {
                throw new ArgumentNullException(nameof({{camelCase Name}}));
            }

            {{/if}}
            {{#if (isVerifyable Type)}}
            if (!{{camelCase Name}}.IsValid)
            {
                throw new ArgumentException("The parameter is not valid", nameof({{camelCase Name}}));
            }

            {{/if}}
            {{/each}}
            {{#each ConstantParameters}}
            const {{typeRef Type}} {{camelCase Name}} = "{{Type.Value}}";
            {{/each}}

            var _path = "{{Path}}";
            {{#each PathParameters}}
            _path = _path.Replace("{ {{~Name~}} }", Client.Serialize({{camelCase Name}}));
            {{/each}}

            var _query = new QueryBuilder();
            {{#each QueryParameters}}
            {{#if IsConstant}}
            _query.Add("{{Name}}", Client.Serialize({{camelCase Name}}));
            {{else}}
            if ({{#notNullCheck Type Required}}{{camelCase Name}}{{/notNullCheck}})
            {
                {{#if IsArray}}
                foreach (var _item in {{camelCase Name}})
                {
                    _query.Add("{{Name}}", Client.Serialize(_item));
                }
                {{else}}
                _query.Add("{{Name}}", Client.Serialize({{camelCase Name}}));
                {{/if}}
            }
            {{/if}}
            {{/each}}

            var _uriBuilder = new UriBuilder(Client.BaseUri);
            _uriBuilder.Path = _uriBuilder.Path.TrimEnd('/') + _path;
            _uriBuilder.Query = _query.ToString();
            var _url = _uriBuilder.Uri;

            HttpRequestMessage _req = null;
            HttpResponseMessage _res = null;
            try
            {
                _req = new HttpRequestMessage({{method HttpMethod}}, _url);
                {{#each HeaderParameters}}

                if ({{#notNullCheck Type Required}}{{camelCase Name}}{{/notNullCheck}})
                {
                    _req.Headers.Add("{{Name}}", {{camelCase Name}});
                }
                {{/each}}
                {{#with BodyParameter}}

                string _requestContent = null;
                if ({{#notNullCheck Type Required}}{{camelCase Name}}{{/notNullCheck}})
                {
                    _requestContent = Client.Serialize({{camelCase Name}});
                    _req.Content = new StringContent(_requestContent, Encoding.UTF8)
                    {
                        Headers =
                        {
                            ContentType = MediaTypeHeaderValue.Parse("application/json; charset=utf-8"),
                        },
                    };
                }
                {{/with}}

                if (Client.Credentials != null)
                {
                    await Client.Credentials.ProcessHttpRequestAsync(_req, cancellationToken).ConfigureAwait(false);
                }

                _res = await Client.SendAsync(_req, cancellationToken).ConfigureAwait(false);
                if (!_res.IsSuccessStatusCode)
                {
                    await On{{Name}}Failed(_req, _res);
                }
                {{#unless ResponseIsFile}}
                string _responseContent = await _res.Content.ReadAsStringAsync().ConfigureAwait(false);
                {{#if ResponseIsVoid}}
                return new HttpOperationResponse
                {{else}}
                return new HttpOperationResponse<{{typeRef ResponseType}}>
                {{/if}}
                {
                    Request = _req,
                    Response = _res,
                    {{#unless ResponseIsVoid}}
                    Body = Client.Deserialize<{{typeRef ResponseType}}>(_responseContent),
                    {{/unless}}
                };
                {{else}}
                System.IO.Stream _responseStream = await _res.Content.ReadAsStreamAsync().ConfigureAwait(false);
                return new HttpOperationResponse<System.IO.Stream>
                {
                    Request = _req,
                    Response = _res,
                    Body = _responseStream
                };
                {{/unless}}
            }
            catch (Exception)
            {
                _req?.Dispose();
                _res?.Dispose();
                throw;
            }
        }
        {{/each}}
    }
}
