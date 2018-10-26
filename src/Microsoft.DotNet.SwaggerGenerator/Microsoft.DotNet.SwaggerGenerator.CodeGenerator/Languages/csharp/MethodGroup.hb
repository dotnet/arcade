﻿using System;
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
        {{#if ResponseIsVoid}}Task{{else}}Task<{{typeRef ResponseType}}>{{/if}} {{Name}}Async(
            {{#each FormalParameters}}
            {{typeRef Type}} {{camelCase Name}}{{#unless Required}} = default{{/unless}},
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
        {{#each Methods}}

        public async Task{{#unless ResponseIsVoid}}<{{typeRef ResponseType}}>{{/unless}} {{Name}}Async(
            {{#each FormalParameters}}
            {{typeRef Type}} {{camelCase Name}}{{#unless Required}} = default{{/unless}},
            {{/each}}
            CancellationToken cancellationToken = default
        )
        {
            using (var _res = await {{Name}}InternalAsync(
                {{#each FormalParameters}}
                {{camelCase Name}},
                {{/each}}
                cancellationToken
            ).ConfigureAwait(false))
            {
                {{#if ResponseIsVoid}}
                return;
                {{else}}
                return _res.Body;
                {{/if}}
            }
        }

        internal async Task<HttpOperationResponse{{#unless ResponseIsVoid}}<{{typeRef ResponseType}}>{{/unless}}> {{Name}}InternalAsync(
            {{#each FormalParameters}}
            {{typeRef Type}} {{camelCase Name}}{{#unless Required}} = default{{/unless}},
            {{/each}}
            CancellationToken cancellationToken = default
        )
        {
            {{#each NonConstantParameters}}
            {{#if Required}}
            if ({{#nullCheck Type}}{{camelCase Name}}{{/nullCheck}})
            {
                throw new ArgumentNullException(nameof({{camelCase Name}}));
            }

            {{/if}}
            {{/each}}
            {{#each VerifyableParameters}}
            if ({{#unless Required}}{{#notNullCheck Type}}{{camelCase Name}}{{/notNullCheck}} && {{/unless}}!{{camelCase Name}}.IsValid)
            {
                throw new ArgumentException("The parameter is not valid", nameof({{camelCase Name}}));
            }

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
            if ({{#notNullCheck Type}}{{camelCase Name}}{{/notNullCheck}})
            {
                _query.Add("{{Name}}", Client.Serialize({{camelCase Name}}));
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

                if ({{#notNullCheck Type}}{{camelCase Name}}{{/notNullCheck}})
                {
                    _req.Headers.Add("{{Name}}", {{camelCase Name}});
                }
                {{/each}}
                {{#with BodyParameter}}

                string _requestContent = null;
                if ({{#notNullCheck Type}}{{camelCase Name}}{{/notNullCheck}})
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
                var _responseContent = await _res.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!_res.IsSuccessStatusCode)
                {
                    {{#if ErrorType}}
                    throw new RestApiException<{{typeRef ErrorType}}>
                    {{else}}
                    throw new RestApiException
                    {{/if}}
                    {
                        Request = new HttpRequestMessageWrapper(_req, {{#if BodyParameter}}_requestContent{{else}}null{{/if}}),
                        Response = new HttpResponseMessageWrapper(_res, _responseContent),
                        {{#if ErrorType}}
                        Body = Client.Deserialize<{{typeRef ErrorType}}>(_responseContent),
                        {{/if}}
                    };
                }
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