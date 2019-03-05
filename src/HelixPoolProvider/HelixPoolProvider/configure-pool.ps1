param (
    $accountUrl,
    $pat,
    $byocUrl,
    $byocProviderName,
	$sharedSecret,
	$project,
	[switch]$skipCreateCloud = $false,
	[switch]$skipCreatePool = $false,
	[switch]$skipCreateAgents = $false,
	$existingAgentPoolId,
	$existingAgentCloudId
)

# Create the VSTS auth header
$base64authinfo = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes(":$pat"))
$vstsAuthHeader = @{"Authorization"="Basic $base64authinfo"}
$allHeaders = $vstsAuthHeader + @{"Content-Type"="application/json"; "Accept"="application/json"}

if (-not $skipCreateCloud) {
	try {
		echo "Creating agent cloud"
		$result = Invoke-WebRequest -Headers $allHeaders -Method POST "$accountUrl/_apis/DistributedTask/AgentClouds?api-version=5.0-preview" -Body "{`"name`":`"$byocProviderName`",`"acquireAgentEndpoint`":`"$byocUrl/acquireagent`",`"releaseAgentEndpoint`":`"$byocUrl/releaseagent`",`"getAgentDefinitionEndpoint`":`"$byocUrl/agentdefinitions`",`"getAgentRequestStatusEndpoint`":`"$byocUrl/status`", `"type`":`"test`", `"sharedSecret`":`"$sharedSecret`"}"
		if ($result.StatusCode -ne 200) {
			echo $result.Content
			throw "Failed to create agent cloud"
		}
	}
	catch {
		throw "Failed to create agent cloud: $_"
	}
	$resultJson = ConvertFrom-Json $result.Content
	$agentCloudId = $resultJson.agentCloudId
} else {
	$agentCloudId = $existingAgentPoolId
}

if (-not $skipCreatePool) {
	try {
		echo "Creating agent pool for cloud"
		$result = Invoke-WebRequest -Headers $allHeaders -Method POST "$accountUrl/_apis/DistributedTask/pools?api-version=5.0-preview" -Body "{`"name`":`"$byocProviderName-Pool`",`"agentCloudId`":$agentCloudId,`"targetSize`":500}"
		if ($result.StatusCode -ne 200) {
			echo $result.Content
			throw "Failed to create pool for agent cloud"
		}
	}
	catch {
		throw "Failed to create agent pool: $_"
	}
	$resultJson = ConvertFrom-Json $result.Content
	$poolProviderId = $resultJson.id
} else {
	$poolProviderId = $existingAgentPoolId
}

if (-not $skipCreateAgents) {
	for ($i=0; $i -lt 100; $i++) {
		try {
			echo "Creating agent $byocProviderName-Agent${i} for pool"
			$result = Invoke-WebRequest -Headers $allHeaders -Method POST "$accountUrl/_apis/DistributedTask/pools/$poolProviderId/agents?api-version=5.0-preview" -Body "{`"name`":`"$byocProviderName-Agent${i}`",`"version`":`"2.138.6`",`"provisioningState`":`"Deallocated`"}"
			if ($result.StatusCode -ne 200) {
				echo $result.Content
				throw "Failed to create agent"
			}
		}
		catch {
			throw "Failed to create agent: $_"
		}
	}
}
