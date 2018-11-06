using System;
using System.Collections.Immutable;
{{#if IsEnum}}
using System.Runtime.Serialization;
{{/if}}
using Newtonsoft.Json;

namespace {{pascalCaseNs Namespace}}.Models
{
    public {{#if IsEnum}}enum{{else}}partial class{{/if}} {{pascalCase Name}}
    {
        {{#if IsEnum}}
        {{#each Values}}
        [EnumMember(Value = "{{this}}")]
        {{pascalCase this}},
        {{/each}}
        {{else}}
        public {{pascalCase Name}}({{#each RequiredAndReadOnlyProperties}}{{typeRef Type}} {{camelCase Name}}{{#unless @last}}, {{/unless}}{{/each}})
        {
            {{#each RequiredAndReadOnlyProperties}}
            {{pascalCase Name}} = {{camelCase Name}};
            {{/each}}
        }
        {{#each Properties}}

        [JsonProperty("{{Name}}")]
        public {{typeRef Type}} {{pascalCase Name}} { get;{{#if ReadOnly}}{{else}} set;{{/if}} }
        {{/each}}
        {{/if}}
        {{#if Verifyable}}

        public bool IsValid
        {
            get
            {
                return
                    {{#each RequiredProperties}}
                    !({{#nullCheck Type}}{{pascalCase Name}}{{/nullCheck}}){{#if @last}};{{else}} &&{{/if}}
                    {{/each}}
            }
        }
        {{/if}}
    }
}
