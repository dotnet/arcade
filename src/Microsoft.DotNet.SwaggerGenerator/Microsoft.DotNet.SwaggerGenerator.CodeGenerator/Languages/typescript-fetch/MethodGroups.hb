/// <reference lib="es2015"/>
/// <reference lib="dom"/>

import moment, { Moment } from "moment";

import * as models from "./models";
import { IServiceClient, RestApiError } from "./service-client";

{{#each MethodGroups}}
{{> MethodGroup this}}

{{/each}}
