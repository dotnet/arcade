export interface I{{pascalCase Name}} {
  {{#each Methods}}
  {{camelCase Name}}(
    {{#each FormalParameters}}
    {{camelCase Name}}{{#if (not Required)}}?{{/if}}: {{typeRef Type 'models.'}},
    {{/each}}
  ): Promise<{{typeRef ResponseType 'models.'}}>;

  {{/each}}
}

export class {{pascalCase Name}} implements I{{pascalCase Name}} {
  constructor(private client: IServiceClient) {
  }

  {{#each Methods}}
  public async {{camelCase Name}}(
    {{#each FormalParameters}}
    {{camelCase Name}}{{#if (not Required)}}?{{/if}}: {{typeRef Type 'models.'}},
    {{/each}}
  ): Promise<{{typeRef ResponseType 'models.'}}> {
    {{#each NonConstantParameters}}
    {{#if Required}}
    if ({{camelCase Name}} == null)
    {
      throw new Error("Required parameter '" + '{{camelCase Name}}' + "' missing.");
    }

    {{/if}}
    {{/each}}
    {{#each VerifyableParameters}}
    if ({{#unless Required}}{{camelCase Name}} != null && {{/unless}}!{{camelCase Name}}.getIsValid())
    {
      throw new Error("Parameter '" + '{{camelCase Name}}' + "' is not valid.");
    }

    {{/each}}

    {{#each ConstantParameters}}
    const {{camelCase Name}} = "{{Value}}";
    {{/each}}

    {{#if PathParameters}}
    const _url = new URL((this.client.baseUri + '{{Path}}')
      {{#each PathParameters}}
      .replace('{' + '{{Name}}' + '}', {{camelCase Name}}.toString())
      {{/each}});
    {{else}}
    const _url = new URL(this.client.baseUri + '{{Path}}');
    {{/if}}

    {{#each QueryParameters}}
    {{#if IsConstant}}
    _url.searchParams.append("{{Name}}", {{serialize Type 'models.'}}{{camelCase Name}}{{/serialize}});
    {{else}}
    if ({{camelCase Name}} != null)
    {
      _url.searchParams.append("{{Name}}", {{serialize Type 'models.'}}{{camelCase Name}}{{/serialize}}.toString());
    }
    {{/if}}
    {{/each}}

    const _request = new Request(
      _url.href,
      {
        method: '{{upperCase HttpMethod.Method}}',
        {{#with BodyParameter}}
        body: JSON.stringify({{serialize Type 'models.'}}{{camelCase Name}}{{/serialize}}),
        {{/with}}
      });
    {{#if BodyParameter}}
    _request.headers.append("Content-Type", "application/json");
    {{/if}}

    {{#each HeaderParameters}}
    {{#if IsConstant}}
    _request.headers.append("{{Name}}", {{serialize Type 'models.'}}{{camelCase Name}}{{/serialize}});
    {{else}}
    if ({{camelCase Name}} != null)
    {
      _request.headers.append("{{Name}}", {{serialize Type 'models.'}}{{camelCase Name}}{{/serialize}}.toString());
    }
    {{/if}}
    {{/each}}

    const _res = await this.client.fetch(_request);

    if (!_res.ok) {
      {{#if ErrorType}}
      {{/if}}
      throw new RestApiError<models.{{typeRef ErrorType}}>(
        `The request returned ${_res.status} ${_res.statusText}`,
        _res,
        {{#if ErrorType}}
        {{deserialize ErrorType 'models.'}}_res.json(){{/deserialize}},
        {{else}}
        undefined,
        {{/if}}
        );
    }

    {{#if (not ResponseIsVoid)}}
    return {{deserialize ResponseType 'models.'}}_res.json(){{/deserialize}};
    {{/if}}
  }

  {{/each}}
}
