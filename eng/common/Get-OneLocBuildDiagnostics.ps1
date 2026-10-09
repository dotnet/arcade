# Requires PowerShell 7; never publish raw logs, responses, or exception messages.
param([Parameter(Mandatory = $true)][string] $OutputDirectory)

$ErrorActionPreference = 'Stop'
. $PSScriptRoot\pipeline-logging-functions.ps1
$report = @{
    collectedAtUtc = [DateTimeOffset]::UtcNow.ToString('o')
    requests = [Collections.Generic.List[object]]::new()
    warnings = [Collections.Generic.List[string]]::new()
}

function Warn([string] $Message) {
    $report.warnings.Add($Message)
    Write-PipelineTelemetryError -Category 'OneLocBuildDiagnostics' -Type warning -Message $Message -Force
}

function Probe([string] $Operation, [string] $Path) {
    $entry = @{
        operation = $Operation; uri = "https://api.github.com/repos/$repository/$Path".TrimEnd('/')
        attempt = 1; status = $null; headers = @{}; sha = $null
    }
    $clock = [Diagnostics.Stopwatch]::StartNew()
    $data = $null
    try {
        $response = Invoke-WebRequest -Uri $entry.uri -Headers $githubHeaders `
            -SkipHttpErrorCheck -MaximumRedirection 0 -TimeoutSec 30
        $entry.status = [int] $response.StatusCode
        foreach ($name in @('X-GitHub-Request-Id', 'Retry-After', 'X-RateLimit-Limit',
                'X-RateLimit-Remaining', 'X-RateLimit-Used', 'X-RateLimit-Reset', 'X-RateLimit-Resource')) {
            if ($response.Headers.ContainsKey($name)) {
                $entry.headers[$name] = $response.Headers[$name] -join ', '
            }
        }
        if ($entry.status -eq 200) {
            $data = $response.Content | ConvertFrom-Json
            $sha = if ($data.object.sha) { $data.object.sha } else { $data.sha }
            if ($sha -match '^[0-9a-f]{40}$') { $entry.sha = $sha }
        }
    }
    catch {
        Warn "$Operation failed ($($_.Exception.GetType().FullName)); exception details omitted."
    }
    finally {
        $entry.elapsedMilliseconds = $clock.ElapsedMilliseconds
        $report.requests.Add($entry)
    }
    return $data
}

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
try {
    if (-not $env:GITHUB_APP_TOKEN -or $env:GITHUB_APP_TOKEN.StartsWith('$(')) {
        Warn 'GitHub App token unavailable; probes skipped.'
        return
    }
    $repository = if ($env:ONELOC_MIRROR_REPO) { "$env:ONELOC_GITHUB_ORG/$env:ONELOC_MIRROR_REPO" }
        elseif ($env:BUILD_REPOSITORY_PROVIDER -eq 'GitHub') { $env:BUILD_REPOSITORY_NAME }
    if ($repository -notmatch '^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$') {
        Warn 'GitHub repository unavailable or invalid; probes skipped.'
        return
    }
    $githubHeaders = @{
        Authorization = "Bearer $env:GITHUB_APP_TOKEN"
        Accept = 'application/vnd.github+json'
        'User-Agent' = 'dotnet-arcade-onelocbuild-diagnostics'
        'X-GitHub-Api-Version' = '2022-11-28'
    }
    $null = Probe 'Read repository' ''
    $collection = [Uri] $env:SYSTEM_COLLECTIONURI
    if (-not $env:SYSTEM_ACCESSTOKEN -or $collection.Scheme -ne 'https' -or
        ($collection.Host -ne 'dev.azure.com' -and $collection.Host -notlike '*.visualstudio.com')) {
        Warn 'Build log credentials or collection URI unavailable or invalid; ref probes skipped.'
        return
    }
    $buildUri = "$($collection.AbsoluteUri.TrimEnd('/'))/$([Uri]::EscapeDataString($env:SYSTEM_TEAMPROJECTID))/_apis/build/builds/$env:BUILD_BUILDID"
    $ado = @{ Headers = @{ Authorization = "Bearer $env:SYSTEM_ACCESSTOKEN" }; MaximumRedirection = 0; TimeoutSec = 30 }
    $timeline = Invoke-RestMethod -Uri "$buildUri/timeline?api-version=7.1" @ado
    $task = $timeline.records | Where-Object {
        $_.type -eq 'Task' -and $_.name -eq 'OneLocBuild' -and
        $_.parentId -eq $env:SYSTEM_JOBID -and $_.result -eq 'failed'
    } | Sort-Object attempt, finishTime -Descending | Select-Object -First 1
    if (-not $task.log.id) { Warn 'No failed OneLocBuild log in this job; ref probes skipped.'; return }
    $log = Invoke-WebRequest -Uri "$buildUri/logs/$($task.log.id)?api-version=7.1" @ado
    $matches = [regex]::Matches($log.Content, "Comparing '(refs/heads/[^'\r\n]+)' and '(refs/heads/[^'\r\n]+)' branches\.\.\.")
    if (-not $matches.Count) { Warn 'No comparison refs in task log; ref probes skipped.'; return }
    $refs = @($matches[-1].Groups[1].Value, $matches[-1].Groups[2].Value)
    $report.refs = $refs
    $shas = foreach ($ref in $refs) {
        $data = Probe "Read $ref" "git/ref/$([Uri]::EscapeDataString($ref.Substring(5)))"
        if ($data.object.sha -match '^[0-9a-f]{40}$') {
            $null = Probe "Read commit for $ref" "git/commits/$($data.object.sha)"
            $data.object.sha
        }
    }
    $null = Probe 'Compare refs' "compare/$([Uri]::EscapeDataString($refs[0]))...$([Uri]::EscapeDataString($refs[1]))?per_page=1"
    if (@($shas).Count -eq 2) {
        $null = Probe 'Compare observed SHAs' "compare/$($shas[0])...$($shas[1])?per_page=1"
    }
    else { Warn 'Both ref SHAs unavailable; SHA comparison skipped.' }
}
catch {
    Warn "Collection incomplete ($($_.Exception.GetType().FullName)); exception details omitted."
}
finally {
    $json = $report | ConvertTo-Json -Depth 6
    foreach ($secret in @($env:GITHUB_APP_TOKEN, $env:SYSTEM_ACCESSTOKEN)) {
        if ($secret) { $json = $json.Replace($secret, '[REDACTED]') }
    }
    $json | Set-Content -LiteralPath (Join-Path $OutputDirectory 'diagnostics.json') -Encoding utf8
    Write-Host $json
}
