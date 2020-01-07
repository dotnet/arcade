using System;
using System.Collections.Immutable;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Core;
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

            var _baseUri = Client.Options.BaseUri;
            var _url = new RequestUriBuilder();
            _url.Reset(_baseUri);
            _url.AppendPath(
                "{{Path}}"
                {{~#each PathParameters~}}
                .Replace("{ {{~Name~}} }", Uri.EscapeDataString(Client.Serialize({{camelCase Name}})))
                {{~/each~}},
                false);

            {{#each QueryParameters}}
            {{#if IsConstant}}
            _url.AppendQuery("{{Name}}", Client.Serialize({{camelCase Name}}));
            {{else}}
            if ({{#notNullCheck Type Required}}{{camelCase Name}}{{/notNullCheck}})
            {
                {{#if IsArray}}
                foreach (var _item in {{camelCase Name}})
                {
                    _url.AppendQuery("{{Name}}", Client.Serialize(_item));
                }
                {{else}}
                _url.AppendQuery("{{Name}}", Client.Serialize({{camelCase Name}}));
                {{/if}}
            }
            {{/if}}
            {{/each}}


            using (var _req = Client.Pipeline.CreateRequest())
            {
                _req.Uri = _url;
                _req.Method = {{method HttpMethod}};
                {{#each HeaderParameters}}

                if ({{#notNullCheck Type Required}}{{camelCase Name}}{{/notNullCheck}})
                {
                    _req.Headers.Add("{{Name}}", {{camelCase Name}});
                }
                {{/each}}
                {{#with BodyParameter}}

                if ({{#notNullCheck Type Required}}{{camelCase Name}}{{/notNullCheck}})
                {
                    _req.Content = RequestContent.Create(Encoding.UTF8.GetBytes(Client.Serialize({{camelCase Name}})));
                    _req.Headers.Add("Content-Type", "application/json; charset=utf-8");
                }
                {{/with}}

                using (var _res = await Client.SendAsync(_req, cancellationToken).ConfigureAwait(false))
                {
                    if (_res.Status < 200 || _res.Status >= 300)
                    {
                        await On{{Name}}Failed(_req, _res).ConfigureAwait(false);
                    }

                    {{#unless ResponseIsVoid}}
                    if (_res.ContentStream == null)
                    {
                        await On{{Name}}Failed(_req, _res).ConfigureAwait(false);
                    }
                    {{/unless}}

                    {{#if ResponseIsFile}}
                    return new ResponseStream(_res.ContentStream, _res);
                    {{else if ResponseIsVoid}}
                    return;
                    {{else}}
                    using (var _reader = new StreamReader(_res.ContentStream))
                    {
                        var _content = await _reader.ReadToEndAsync().ConfigureAwait(false);
                        var _body = Client.Deserialize<{{typeRef ResponseType}}>(_content);
                        {{#if Paginated}}
                        return new PagedResponse<{{typeRef ResponseType.BaseType}}>(Client, On{{Name}}Failed, _res, _body);
                        {{else}}
                        return _body;
                        {{/if}}
                    }
                    {{/if}}
                }
            }
        }

        internal async Task On{{Name}}Failed(Request req, Response res)
        {
            string content = null;
            if (res.ContentStream != null)
            {
                using (var reader = new StreamReader(res.ContentStream))
                {
                    content = await reader.ReadToEndAsync().ConfigureAwait(false);
                }
            }

            var ex = new RestApiException{{#if ErrorType}}<{{typeRef ErrorType}}>{{/if}}(
                req,
                res,
                content
                {{~#if ErrorType}},
                Client.Deserialize<{{typeRef ErrorType}}>(content)
                {{/if~}});
            HandleFailed{{Name}}Request(ex);
            HandleFailedRequest(ex);
            Client.OnFailedRequest(ex);
            throw ex;
        }
        {{/each}}
    }
}
