
# Helix deployment guidance with OS Onboarding Azure DevOps Agents

## Step-by-step

1. After The [build and deploy production](https://dev.azure.com/dnceng/internal/_build?definitionId=145&_a=summary) build definition completes, the scalesets will be updated, but the active machines (Azure DevOps agents) will not be updated.

2. To update the active agents, you must perform the following steps either via the [web UI](https://dev.azure.com/dnceng/_settings/agentpools) or via the [REST API](#azure-devops-agent-rest-apis):

    a. [Disable](#disable-agent) all of the active machines in the queue you want to update.

    b. Wait for the machines to finish [building](#list-agents)

    c. Scale down the existing scale set via https://resources.azure.com (the scaleset will scale back up within 15 minutes, or you can specify the value explicitly)

    d. [Delete](#delete-agent) unused agents

## Azure DevOps Agent REST API's

| Pool                       | Pool ID |
| -------------------------- | ------- |
| dnceng-linux-external-temp | 32 |
| dnceng-linux-internal-temp | 28 |
| dotnet-external-temp       | 19 |
| dotnet-internal-temp       | 33 |

### List agents

`curl -u [username]:[pat] --request GET https://dev.azure.com/dnceng/_apis/distributedtask/pools/[Pool ID]/agents?includeCapabilities=false&includeAssignedRequest=true`

If does not have an 'assignedRequest' block, then agent is idle.

### Disable agent

`curl -u [username]:[pat] --request PATCH https://dev.azure.com/dnceng/_apis/distributedtask/pools/[Pool ID]/agents/[Agent ID] -H "Accept: application/json;api-version=5.0-preview.1" -H "content-type:application/json" --data @data.txt`

data.txt

```JSON
{enabled: false, id: [Agent ID]}
```

### Delete agent

`curl -u [username]:[pat] --request DELETE https://dev.azure.com/dnceng/_apis/distributedtask/pools/[Pool ID]/agents/[Agent ID] -H "Accept: application/json;api-version=5.0-preview.1" -H "content-type:application/json"`