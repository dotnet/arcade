import { IServiceClient } from "./service-client";

export * from "./models";
export * from "./method-groups";
import * as methods from "./method-groups";

export class {{pascalCase Name}} implements IServiceClient {
  private _baseUri: string;

  constructor(baseUri?: string) {
    this._baseUri = baseUri || "{{scheme}}://{{Host}}/";
    {{#each MethodGroups}}
    this._{{camelCase Name}} = new methods.{{pascalCase Name}}(this);
    {{/each}}
  }

  public get baseUri(): string {
    return this._baseUri;
  }

  {{#each MethodGroups}}
  private _{{camelCase Name}}: methods.I{{pascalCase Name}};
  public get {{pascalCase Name}}(): methods.I{{pascalCase Name}} {
    return this._{{camelCase Name}};
  }

  {{/each}}

  private _fetchFn: any = fetch;
  async fetch(input: RequestInfo, init?: RequestInit): Promise<Response> {
    if (!this._fetchFn) {
        this._fetchFn = await import("isomorphic-fetch");
    }
    return await this._fetchFn.call(null, input, init);
  }
}
