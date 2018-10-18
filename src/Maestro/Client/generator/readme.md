# Helix Client Library Generator

New client library code can be generated with `npm start`

## Configuration

> see https://aka.ms/autorest

```yaml
# Fix versions of autorest extensions
use:
- '@microsoft.azure/autorest.csharp@2.2.51'

input-file: https://maestro-prod.westus2.cloudapp.azure.com/api/swagger.json
sync-methods: none
add-credentials: true
override-client-name: MaestroApi
license-header: |
  Licensed to the .NET Foundation under one or more agreements.
  The .NET Foundation licenses this file to you under the MIT license.
  See the LICENSE file in the project root for more information.

directive:
- where: "$.definitions.*.properties.*.readOnly"
  set: false
- where: "$.schemes[0]"
  set: https
- where: "$.host"
  set: localhost:4430
output-artifact:
- swagger-document.json
csharp:
  output-folder: ../src/Generated
  namespace: Microsoft.DotNet.Maestro.Client
  use-datetimeoffset: true
  use-internal-constructors: true
```
