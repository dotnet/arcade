export {{#if IsEnum}}enum{{else}}class{{/if}} {{pascalCase Name}} {
  {{#if IsEnum}}
  {{#each Values}}
  {{pascalCase this}} = "{{this}}",
  {{/each}}
  {{else}}

  static deserialize(input: any): {{pascalCase Name}} {
    const result = new {{pascalCase Name}}(
      {{#each Properties}}
      {{#if (or Required ReadOnly)}}
      {{deserialize Type ''}}input['{{Name}}']{{/deserialize}},
      {{/if}}
      {{/each}}
    )
    {{#each Properties}}
    {{#if (and (not Required) (not ReadOnly))}}
    result.{{camelCase Name}} = {{deserialize Type ''}}input['{{Name}}']{{/deserialize}};
    {{/if}}
    {{/each}}
    return result;
  }

  static serialize(input?: {{pascalCase Name}}): any {
    if (!input) {
      return undefined;
    }
    const result = {
      {{#each Properties}}
      '{{Name}}': {{serialize Type ''}}input.{{camelCase Name}}{{/serialize}},
      {{/each}}
    };
    return result;
  }

  constructor(
    {{#each Properties}}
    {{#if (or Required ReadOnly)}}
    {{camelCase Name}}: {{typeRef Type}}{{#if (not Required)}} | undefined{{/if}},
    {{/if}}
    {{/each}}
  )
  {
    {{#each Properties}}
    {{#if (or Required ReadOnly)}}
    this._{{camelCase Name}} = {{camelCase Name}};
    {{/if}}
    {{/each}}
  }
  {{#each Properties}}

  private _{{camelCase Name}}{{#if (not Required)}}?{{/if}}: {{typeRef Type}};

  public get {{camelCase Name}}(): {{typeRef Type}}{{#if (not Required)}} | undefined{{/if}} {
    return this._{{camelCase Name}};
  }
  {{#if (not ReadOnly)}}

  public set {{camelCase Name}}(value: {{typeRef Type}}{{#if (not Required)}} | undefined{{/if}}) {
    this._{{camelCase Name}} = value;
  }
  {{/if}}

  {{/each}}
  {{#if Verifyable}}

  public getIsValid(): boolean {
    return (
      {{#each RequiredProperties}}
      this._{{camelCase Name}} != null{{#if @last}});{{else}} &&{{/if}}
      {{/each}}
  }
  {{/if}}
  {{/if}}
}
