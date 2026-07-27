#!/usr/bin/env pwsh
<#
.DESCRIPTION
Pester tests for the inter-branch merge flow.

These cover 'read-configuration.ps1' together with the branch resolution that lives inline in
'inter-branch-merge-base.yml'. That resolution deliberately stays inline in the workflow rather than
moving into a script, because 'script_version' selects the checked out scripts independently of the
ref the caller pins the workflow to.

Nothing about the workflow is restated here. The step bodies, the step 'env:' mappings, the
'working-directory' values and the 'if:' gates are all read out of the shipped YAML and then
executed, so the workflow and these tests cannot drift apart. 'inter-branch-merge.ps1' is replaced
by a stub that reuses the real script's own parameter block and records what it was bound to, which
is how the tests observe the command line the workflow actually builds.

Run with:
    Invoke-Pester -Path .github/workflows/scripts/tests
#>

BeforeAll {
    $script:WorkflowsRoot = (Resolve-Path (Join-Path (Join-Path $PSScriptRoot '..') '..')).Path
    $script:WorkflowPath = Join-Path $script:WorkflowsRoot 'inter-branch-merge-base.yml'
    $script:ScriptsRoot = Join-Path $script:WorkflowsRoot 'scripts'
    $script:WorkflowLines = [System.IO.File]::ReadAllLines($script:WorkflowPath)

    #region workflow parsing
    # Only the pieces of YAML these tests need are parsed, so that they do not take a dependency on
    # a YAML module being present on the runner. Every helper throws rather than returning something
    # empty, so that a parsing miss can never turn into an assertion that passes vacuously.

    function Get-WorkflowStepLines([string]$StepName) {
        $lines = $script:WorkflowLines
        $nameIndexes = @()
        for ($i = 0; $i -lt $lines.Count; $i++) {
            if ($lines[$i] -match "^\s*-?\s*name:\s*$([regex]::Escape($StepName))\s*$") {
                $nameIndexes += $i
            }
        }
        if ($nameIndexes.Count -ne 1) {
            throw "Expected exactly one step named '$StepName' in $($script:WorkflowPath) but found $($nameIndexes.Count)."
        }

        # Walk back to the '- ' that opens the step, then forward to the next step at that level.
        $start = $nameIndexes[0]
        while ($start -gt 0 -and $lines[$start] -notmatch '^(\s*)-\s') { $start-- }
        if ($lines[$start] -notmatch '^(\s*)-\s') { throw "Could not find the start of step '$StepName'." }
        $markerIndent = $Matches[1].Length

        $end = $lines.Count - 1
        for ($i = $start + 1; $i -lt $lines.Count; $i++) {
            if ($lines[$i] -match '^(\s*)-\s' -and $Matches[1].Length -le $markerIndent) {
                $end = $i - 1
                break
            }
        }

        return $lines[$start..$end]
    }

    # Returns the step's 'run:' along with whether it was written as a block scalar. Block bodies are
    # already plain PowerShell; single line commands still contain workflow expressions.
    function Get-WorkflowStepRun([string]$StepName) {
        $stepLines = Get-WorkflowStepLines $StepName

        for ($i = 0; $i -lt $stepLines.Count; $i++) {
            if ($stepLines[$i] -match '^(\s*)-?\s*run:\s*\|\s*$') {
                $runIndent = $Matches[1].Length
                $body = @()
                for ($j = $i + 1; $j -lt $stepLines.Count; $j++) {
                    $line = $stepLines[$j]
                    if ($line.Trim().Length -eq 0) {
                        $body += ''
                        continue
                    }
                    $indent = $line.Length - $line.TrimStart().Length
                    if ($indent -le $runIndent) { break }
                    $body += $line.Substring($runIndent + 2)
                }

                $text = ($body -join "`n")
                if ([string]::IsNullOrWhiteSpace($text)) {
                    throw "Extracted an empty block scalar 'run:' for step '$StepName'."
                }
                return [pscustomobject]@{ IsBlock = $true; Text = $text }
            }

            if ($stepLines[$i] -match '^\s*-?\s*run:\s*(\S.*?)\s*$') {
                return [pscustomobject]@{ IsBlock = $false; Text = $Matches[1] }
            }
        }

        throw "Step '$StepName' has no 'run:'."
    }

    function Get-WorkflowStepEnvironment([string]$StepName) {
        $stepLines = Get-WorkflowStepLines $StepName

        $envIndex = -1
        $envIndent = 0
        for ($i = 0; $i -lt $stepLines.Count; $i++) {
            if ($stepLines[$i] -match '^(\s*)env:\s*$') {
                $envIndex = $i
                $envIndent = $Matches[1].Length
                break
            }
        }
        if ($envIndex -lt 0) { return @{} }

        $result = @{}
        for ($i = $envIndex + 1; $i -lt $stepLines.Count; $i++) {
            $line = $stepLines[$i]
            if ($line.Trim().Length -eq 0) { continue }
            $indent = $line.Length - $line.TrimStart().Length
            if ($indent -le $envIndent) { break }

            if ($line -match '^\s*([A-Za-z_][A-Za-z0-9_]*):\s*(.*?)\s*$') { $result[$Matches[1]] = $Matches[2] }
            else { throw "Unrecognised 'env:' entry '$line' in step '$StepName'." }
        }

        return $result
    }

    function Get-WorkflowStepWorkingDirectory([string]$StepName) {
        foreach ($line in (Get-WorkflowStepLines $StepName)) {
            if ($line -match '^\s*working-directory:\s*(.+?)\s*$') { return $Matches[1] }
        }
        return $null
    }

    function Get-WorkflowStepCondition([string]$StepName) {
        foreach ($line in (Get-WorkflowStepLines $StepName)) {
            if ($line -match '^\s*if:\s*(.+?)\s*$') { return $Matches[1] }
        }
        throw "Step '$StepName' has no 'if:' gate. The gate assertions would otherwise pass vacuously."
    }

    # Substitutes workflow expressions from a flat context keyed by the expression text. Anything the
    # context does not know about throws, so a rewired step cannot quietly keep the old value.
    function Resolve-WorkflowExpression([string]$Text, [hashtable]$Context) {
        $evaluator = {
            param($match)

            $expression = $match.Groups[1].Value
            if (-not $Context.ContainsKey($expression)) {
                throw "Unresolved workflow expression '$expression'. Add it to the test context if the workflow gained a new reference."
            }
            return [string]$Context[$expression]
        }

        return [regex]::Replace($Text, '\$\{\{\s*(.+?)\s*\}\}', $evaluator)
    }

    # Evaluates the subset of the GitHub Actions expression language the gates use. Anything richer
    # throws, so that a future gate change cannot silently escape this suite.
    function Test-ActionsCondition([string]$Expression, [hashtable]$Outputs) {
        if ([string]::IsNullOrWhiteSpace($Expression)) {
            throw 'An empty gate expression was supplied. Refusing to report it as satisfied.'
        }

        foreach ($term in ($Expression -split '\s*&&\s*')) {
            if ($term -notmatch "^steps\.([A-Za-z0-9_\-]+)\.outputs\.([A-Za-z0-9_\-]+)\s*(==|!=)\s*'([^']*)'$") {
                throw "Unsupported expression term '$term'. Update Test-ActionsCondition if the gates changed."
            }

            $key = "$($Matches[1]).$($Matches[2])"
            $operator = $Matches[3]
            $expected = $Matches[4]

            $actual = ''
            if ($Outputs.ContainsKey($key) -and $null -ne $Outputs[$key]) { $actual = $Outputs[$key] }

            # GitHub Actions compares strings case insensitively.
            $equal = [string]::Equals($actual, $expected, [System.StringComparison]::OrdinalIgnoreCase)
            if (($operator -eq '==') -and (-not $equal)) { return $false }
            if (($operator -eq '!=') -and $equal) { return $false }
        }

        return $true
    }
    #endregion

    #region execution
    function ConvertFrom-GitHubOutput([string]$Path) {
        $result = @{}
        if (-not (Test-Path -LiteralPath $Path)) { return $result }
        foreach ($line in (Get-Content -LiteralPath $Path)) {
            $pair = $line -split '=', 2
            if ($pair.Count -eq 2) { $result[$pair[0]] = $pair[1] }
        }
        return $result
    }

    # The workflow runs on windows-latest and spells the script paths with backslashes. Nothing else
    # in those command lines uses a backslash, so normalising them lets the real command run here too.
    function ConvertTo-LocalCommand([string]$Command) {
        if ([System.IO.Path]::DirectorySeparatorChar -eq '\') { return $Command }
        return ($Command -replace '\\', '/')
    }

    function Invoke-InChildPwsh([string]$Script, [hashtable]$EnvironmentVariables, [string]$WorkingDirectory) {
        $scriptFile = Join-Path ([System.IO.Path]::GetTempPath()) ("ibm-step-$([guid]::NewGuid().ToString('N')).ps1")
        $outputFile = Join-Path ([System.IO.Path]::GetTempPath()) ("ibm-output-$([guid]::NewGuid().ToString('N')).txt")
        $uriFile = Join-Path ([System.IO.Path]::GetTempPath()) ("ibm-uri-$([guid]::NewGuid().ToString('N')).txt")
        $errorFile = Join-Path ([System.IO.Path]::GetTempPath()) ("ibm-error-$([guid]::NewGuid().ToString('N')).txt")

        # The child renders errors with PowerShell's ConciseView, which wraps long messages across
        # gutter prefixed lines depending on console width. Record the raw message instead, so
        # assertions match what was actually thrown rather than how it happened to be formatted.
        $catchBlock = @'
}
catch {
    $_.Exception.Message | Out-File -FilePath $env:IBM_TEST_ERROR -Append
    exit 1
}
'@
        Set-Content -LiteralPath $scriptFile -Value ("try {`n" + $Script + "`n" + $catchBlock) -Encoding utf8
        New-Item -ItemType File -Path $outputFile -Force | Out-Null

        $all = @{
            'GITHUB_OUTPUT'          = $outputFile
            'IBM_TEST_REQUESTED_URI' = $uriFile
            'IBM_TEST_ERROR'         = $errorFile
        }
        foreach ($key in $EnvironmentVariables.Keys) { $all[$key] = $EnvironmentVariables[$key] }

        $toRestore = @{}
        foreach ($key in $all.Keys) {
            $toRestore[$key] = [System.Environment]::GetEnvironmentVariable($key)
            [System.Environment]::SetEnvironmentVariable($key, $all[$key])
        }

        try {
            Push-Location $WorkingDirectory
            try {
                $log = & pwsh -NoProfile -NonInteractive -File $scriptFile 2>&1 | Out-String
                $exitCode = $LASTEXITCODE
            }
            finally { Pop-Location }
        }
        finally {
            foreach ($key in $toRestore.Keys) { [System.Environment]::SetEnvironmentVariable($key, $toRestore[$key]) }
        }

        $outputs = ConvertFrom-GitHubOutput $outputFile
        $requestedUris = @(if (Test-Path -LiteralPath $uriFile) { Get-Content -LiteralPath $uriFile })
        $errorMessage = if (Test-Path -LiteralPath $errorFile) { (Get-Content -LiteralPath $errorFile -Raw) } else { '' }
        Remove-Item -LiteralPath $scriptFile, $outputFile -Force -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath $uriFile, $errorFile -Force -ErrorAction SilentlyContinue

        return [pscustomobject]@{
            ExitCode      = $exitCode
            Outputs       = $outputs
            Log           = $log
            ErrorMessage  = $errorMessage
            RequestedUris = $requestedUris
        }
    }

    # Runs a real workflow step: its body, its 'env:' mapping and its 'working-directory' all come
    # from the YAML, and only the surrounding context is supplied by the test.
    function Invoke-WorkflowStep {
        param(
            [string]$StepName,
            [hashtable]$Context,
            [hashtable]$ExtraEnvironment = @{},
            [string]$Preamble = ''
        )

        $run = Get-WorkflowStepRun $StepName
        $body = if ($run.IsBlock) { $run.Text } else { ConvertTo-LocalCommand (Resolve-WorkflowExpression $run.Text $Context) }

        $environment = @{}
        foreach ($entry in (Get-WorkflowStepEnvironment $StepName).GetEnumerator()) {
            $environment[$entry.Key] = Resolve-WorkflowExpression $entry.Value $Context
        }
        foreach ($entry in $ExtraEnvironment.GetEnumerator()) { $environment[$entry.Key] = $entry.Value }

        $workingDirectory = Get-WorkflowStepWorkingDirectory $StepName
        $workingDirectory = if ($workingDirectory) {
            Resolve-WorkflowExpression $workingDirectory $Context
        }
        else {
            # Actions defaults a step's working directory to github.workspace.
            $Context['github.workspace']
        }

        $script = if ($Preamble) { $Preamble + "`n" + $body } else { $body }
        return Invoke-InChildPwsh -Script $script -EnvironmentVariables $environment -WorkingDirectory $workingDirectory
    }
    #endregion

    #region fixtures
    $script:ReadConfigurationCondition = Get-WorkflowStepCondition 'Read configuration status'
    $script:MergeTargetCondition = Get-WorkflowStepCondition 'Merge target status'
    $script:MergeCondition = Get-WorkflowStepCondition 'Merge branches'

    $script:Configuration = @'
{
  "merge-flow-configurations": {
    "main": {
      "MergeToBranch": "release/next",
      "ExtraSwitches": "-QuietComments",
      "ResetToTargetPaths": [ "eng/Version.Details.xml", "global.json" ]
    },
    "release/9.0.1xx": {
      "MergeToBranch": "main",
      "ExtraSwitches": "-AllowAutomatedCommits"
    },
    "policy-only": {
      "ExtraSwitches": "-AllowAutomatedCommits -QuietComments",
      "ResetToTargetPaths": [ "eng/Version.Details.xml" ]
    },
    "empty-target": {
      "MergeToBranch": ""
    }
  }
}
'@

    $script:ScratchRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('ibm-' + [guid]::NewGuid().ToString('N'))
    $script:Workspace = Join-Path $script:ScratchRoot 'workspace'
    $script:RepositoryPath = Join-Path $script:Workspace 'repository'
    $stagedScripts = Join-Path (Join-Path (Join-Path (Join-Path (Join-Path $script:Workspace 'arcade-repository') '.github') 'workflows') 'scripts') ''
    New-Item -ItemType Directory -Path $stagedScripts -Force | Out-Null

    # The workflow invokes the scripts from the arcade checkout, so stage them where it expects them.
    Copy-Item -LiteralPath (Join-Path $script:ScriptsRoot 'read-configuration.ps1') -Destination $stagedScripts -Force

    # 'inter-branch-merge.ps1' talks to git and to the GitHub API, so stand in a stub that reuses the
    # real script's own parameter block. Reusing it means the stub binds arguments exactly as the
    # real script would, so a workflow that builds a command line the real script would reject fails
    # here too.
    $mergeScriptAst = [System.Management.Automation.Language.Parser]::ParseFile(
        (Join-Path $script:ScriptsRoot 'inter-branch-merge.ps1'), [ref]$null, [ref]$null)
    $mergeParameterBlock = $mergeScriptAst.ParamBlock.Extent.Text
    if ([string]::IsNullOrWhiteSpace($mergeParameterBlock)) { throw 'Could not read the parameter block of inter-branch-merge.ps1.' }

    $mergeRecorder = @'

"invokedRepoOwner=$RepoOwner" | Out-File -FilePath $env:GITHUB_OUTPUT -Append
"invokedRepoName=$RepoName" | Out-File -FilePath $env:GITHUB_OUTPUT -Append
"invokedMergeFromBranch=$MergeFromBranch" | Out-File -FilePath $env:GITHUB_OUTPUT -Append
"invokedMergeToBranch=$MergeToBranch" | Out-File -FilePath $env:GITHUB_OUTPUT -Append
"invokedResetToTargetPaths=$ResetToTargetPaths" | Out-File -FilePath $env:GITHUB_OUTPUT -Append
"invokedAllowAutomatedCommits=$($AllowAutomatedCommits.IsPresent)" | Out-File -FilePath $env:GITHUB_OUTPUT -Append
"invokedQuietComments=$($QuietComments.IsPresent)" | Out-File -FilePath $env:GITHUB_OUTPUT -Append
'@

    Set-Content -LiteralPath (Join-Path $stagedScripts 'inter-branch-merge.ps1') -Value ($mergeParameterBlock + "`n" + $mergeRecorder) -Encoding utf8

    # A throwaway repository with an 'origin' remote, so the existence checks in the resolution step
    # run against real refs rather than being stubbed out.
    $upstream = Join-Path $script:ScratchRoot 'upstream'
    New-Item -ItemType Directory -Path $upstream -Force | Out-Null
    Push-Location $upstream
    try {
        & git init --quiet --initial-branch main . 2>&1 | Out-Null
        & git config user.email 'test@example.com'
        & git config user.name 'test'
        Set-Content -LiteralPath (Join-Path $upstream 'file.txt') -Value 'contents'
        & git add . 2>&1 | Out-Null
        & git commit --quiet -m 'initial' 2>&1 | Out-Null
        foreach ($branch in @('release/9.0.1xx', 'release/next', 'release/override', 'policy-only', 'empty-target')) {
            & git branch $branch 2>&1 | Out-Null
        }
    }
    finally { Pop-Location }

    & git clone --quiet $upstream $script:RepositoryPath 2>&1 | Out-Null
    Push-Location $script:RepositoryPath
    try { & git fetch --quiet origin '+refs/heads/*:refs/remotes/origin/*' 2>&1 | Out-Null }
    finally { Pop-Location }

    # 'read-configuration.ps1' fetches the configuration over the network, so shadow
    # Invoke-WebRequest in the scope that invokes it. No real request can be made. The shim mirrors
    # the parameters the real call passes and records the URI, so the branch, path, owner and
    # repository the workflow wires into the lookup are observable.
    $script:ConfigurationFile = Join-Path $script:ScratchRoot 'github-merge-flow.jsonc'
    Set-Content -LiteralPath $script:ConfigurationFile -Value $script:Configuration -Encoding utf8
    $script:ReadConfigurationPreamble = @'
function Invoke-WebRequest {
    param(
        [Parameter(Position = 0)] $Uri,
        [switch] $UseBasicParsing,
        $Method,
        $MaximumRetryCount,
        $Headers
    )

    "$Uri" | Out-File -FilePath $env:IBM_TEST_REQUESTED_URI -Append
    return [pscustomobject]@{ Content = (Get-Content -LiteralPath $env:IBM_TEST_CONFIGURATION_FILE -Raw) }
}
'@

    # Every component of the configuration URL is a distinct value, so swapping any two of them in
    # the workflow changes the URL the tests observe.
    $script:ExpectedConfigurationUri = 'https://raw.githubusercontent.com/dotnet/test-repo/configuration-source-branch/eng/github-merge-flow.jsonc'

    function New-WorkflowContext([hashtable]$Overrides) {
        $context = @{
            'github.workspace'                                    = $script:Workspace
            'github.repository_owner'                             = 'dotnet'
            'secrets.GITHUB_TOKEN'                                = 'not-a-real-token'
            'steps.fetch-repo-name.outputs.repository_name'       = 'test-repo'
            'inputs.configuration_file_branch'                    = 'configuration-source-branch'
            'inputs.configuration_file_path'                      = 'eng/github-merge-flow.jsonc'
            'inputs.merge_from_branch'                            = ''
            'inputs.merge_to_branch'                              = ''
            'steps.resolve-branches.outputs.mergeFromBranch'      = ''
            'steps.resolve-branches.outputs.mergeToBranchOverride' = ''
            'steps.extract-configuration-values.outputs.mergeToBranch'        = ''
            'steps.extract-configuration-values.outputs.configurationFound'   = ''
            'steps.extract-configuration-values.outputs.policyFound'          = ''
            'steps.extract-configuration-values.outputs.resetToTargetPaths'   = ''
            'steps.extract-configuration-values.outputs.mergeSwitchArguments' = ''
            'steps.resolve-merge-target.outputs.mergeToBranch'                = ''
            'steps.resolve-merge-target.outputs.configurationFound'           = ''
        }
        if ($Overrides) {
            foreach ($entry in $Overrides.GetEnumerator()) { $context[$entry.Key] = $entry.Value }
        }
        return $context
    }

    function Add-StepOutputs([hashtable]$Context, [string]$StepId, [hashtable]$Outputs) {
        foreach ($entry in $Outputs.GetEnumerator()) {
            $Context["steps.$StepId.outputs.$($entry.Key)"] = $entry.Value
        }
    }

    function Invoke-ResolveBranches([string]$MergeFromInput, [string]$MergeToInput, [string]$RefName = 'main') {
        $context = New-WorkflowContext @{
            'inputs.merge_from_branch' = $MergeFromInput
            'inputs.merge_to_branch'   = $MergeToInput
        }
        return Invoke-WorkflowStep -StepName 'Resolve merge branches' -Context $context -ExtraEnvironment @{ 'GITHUB_REF_NAME' = $RefName }
    }

    function Invoke-ReadConfiguration([string]$MergeFromBranch) {
        $context = New-WorkflowContext @{ 'steps.resolve-branches.outputs.mergeFromBranch' = $MergeFromBranch }
        return Invoke-WorkflowStep -StepName 'Extract configuration values' -Context $context `
            -Preamble $script:ReadConfigurationPreamble `
            -ExtraEnvironment @{ 'IBM_TEST_CONFIGURATION_FILE' = $script:ConfigurationFile }
    }

    function Invoke-ResolveTarget([hashtable]$ConfigurationOutputs, [string]$Override) {
        $context = New-WorkflowContext @{ 'steps.resolve-branches.outputs.mergeToBranchOverride' = $Override }
        Add-StepOutputs $context 'extract-configuration-values' $ConfigurationOutputs
        return Invoke-WorkflowStep -StepName 'Resolve merge target' -Context $context
    }

    # Errors are recorded raw by the child rather than printed, so the console log can be empty when
    # a step fails. Report both so a failure never surfaces without a reason.
    function Get-StepFailureDetail($result) {
        return ((@($result.ErrorMessage, $result.Log) | Where-Object { $_ -and $_.Trim() }) -join "`n")
    }

    # Drives every step of the job in order, exactly as the workflow wires them together, and reports
    # what the shipped gates decided plus what 'inter-branch-merge.ps1' was actually invoked with.
    function Invoke-MergeFlow([string]$MergeFromInput, [string]$MergeToInput, [string]$RefName = 'main') {
        $context = New-WorkflowContext @{
            'inputs.merge_from_branch' = $MergeFromInput
            'inputs.merge_to_branch'   = $MergeToInput
        }

        $branches = Invoke-WorkflowStep -StepName 'Resolve merge branches' -Context $context -ExtraEnvironment @{ 'GITHUB_REF_NAME' = $RefName }
        if ($branches.ExitCode -ne 0) { throw "Resolve merge branches failed: $(Get-StepFailureDetail $branches)" }
        Add-StepOutputs $context 'resolve-branches' $branches.Outputs

        $configuration = Invoke-WorkflowStep -StepName 'Extract configuration values' -Context $context `
            -Preamble $script:ReadConfigurationPreamble `
            -ExtraEnvironment @{ 'IBM_TEST_CONFIGURATION_FILE' = $script:ConfigurationFile }
        if ($configuration.ExitCode -ne 0) { throw "Extract configuration values failed: $(Get-StepFailureDetail $configuration)" }
        Add-StepOutputs $context 'extract-configuration-values' $configuration.Outputs

        $target = Invoke-WorkflowStep -StepName 'Resolve merge target' -Context $context
        if ($target.ExitCode -ne 0) { throw "Resolve merge target failed: $(Get-StepFailureDetail $target)" }
        Add-StepOutputs $context 'resolve-merge-target' $target.Outputs

        $gateOutputs = @{}
        foreach ($entry in $target.Outputs.GetEnumerator()) { $gateOutputs["resolve-merge-target.$($entry.Key)"] = $entry.Value }

        $merges = Test-ActionsCondition $script:MergeCondition $gateOutputs
        $invocation = $null
        if ($merges) {
            $invocation = Invoke-WorkflowStep -StepName 'Merge branches' -Context $context -ExtraEnvironment @{ 'GITHUB_REF_NAME' = $RefName }
            if ($invocation.ExitCode -ne 0) { throw "Merge branches failed: $(Get-StepFailureDetail $invocation)" }
        }

        return [pscustomobject]@{
            ResolvedFromBranch   = $branches.Outputs['mergeFromBranch']
            ResolvedToBranch     = $target.Outputs['mergeToBranch']
            ConfigurationOutputs = $configuration.Outputs
            ConfigurationUris    = $configuration.RequestedUris
            Merges               = $merges
            ReportsMissingConfig = Test-ActionsCondition $script:ReadConfigurationCondition $gateOutputs
            ReportsMissingTarget = Test-ActionsCondition $script:MergeTargetCondition $gateOutputs
            Invocation           = if ($invocation) { $invocation.Outputs } else { $null }
        }
    }
    #endregion
}

AfterAll {
    if ($script:ScratchRoot -and (Test-Path -LiteralPath $script:ScratchRoot)) {
        Remove-Item -LiteralPath $script:ScratchRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Describe 'read-configuration.ps1' {
    It 'emits the merge target and the policy for a complete entry' {
        $result = Invoke-ReadConfiguration -MergeFromBranch 'main'

        $result.ExitCode | Should -Be 0
        $result.Outputs['mergeToBranch'] | Should -Be 'release/next'
        $result.Outputs['mergeSwitchArguments'] | Should -Be '-QuietComments'
        $result.Outputs['resetToTargetPaths'] | Should -Be 'eng/Version.Details.xml;global.json'
        $result.Outputs['configurationFound'] | Should -Be 'True'
        $result.Outputs['policyFound'] | Should -Be 'True'
    }

    It 'requests the configuration from the branch, path, owner and repository the workflow supplies' {
        $result = Invoke-ReadConfiguration -MergeFromBranch 'main'

        $result.RequestedUris.Count | Should -Be 1
        $result.RequestedUris[0] | Should -Be $script:ExpectedConfigurationUri
    }

    It 'emits the policy but no merge target for an entry without MergeToBranch' {
        $result = Invoke-ReadConfiguration -MergeFromBranch 'policy-only'

        $result.ExitCode | Should -Be 0
        $result.Outputs['policyFound'] | Should -Be 'True'
        $result.Outputs['mergeToBranch'] | Should -Be ''
        $result.Outputs['mergeSwitchArguments'] | Should -Be '-AllowAutomatedCommits -QuietComments'
        $result.Outputs['resetToTargetPaths'] | Should -Be 'eng/Version.Details.xml'
    }

    # 'script_version' selects the scripts independently of the ref the workflow is pinned to, so a
    # workflow pinned to an older ref can run this script. That older workflow gates purely on
    # 'configurationFound' and would expand an empty target into a broken command line, so
    # 'configurationFound' must keep meaning "this entry can be merged on its own".
    It 'withholds configurationFound when there is no merge target, so older pinned workflows still skip' {
        $result = Invoke-ReadConfiguration -MergeFromBranch 'policy-only'

        $result.Outputs.ContainsKey('configurationFound') | Should -BeFalse
    }

    It 'withholds configurationFound when MergeToBranch is present but empty' {
        $result = Invoke-ReadConfiguration -MergeFromBranch 'empty-target'

        $result.Outputs.ContainsKey('configurationFound') | Should -BeFalse
        $result.Outputs['policyFound'] | Should -Be 'True'
    }

    It 'emits nothing when no entry matches the source branch' {
        $result = Invoke-ReadConfiguration -MergeFromBranch 'not-configured'

        $result.ExitCode | Should -Be 0
        $result.Outputs.Count | Should -Be 0
    }

    It 'defaults the policy outputs to empty when the entry omits them' {
        $result = Invoke-ReadConfiguration -MergeFromBranch 'release/9.0.1xx'

        $result.Outputs['mergeSwitchArguments'] | Should -Be '-AllowAutomatedCommits'
        $result.Outputs['resetToTargetPaths'] | Should -Be ''
    }
}

Describe 'Resolve merge branches step' {
    It 'falls back to the triggering branch when no source is supplied' {
        $result = Invoke-ResolveBranches -MergeFromInput '' -MergeToInput '' -RefName 'main'

        $result.ExitCode | Should -Be 0
        $result.Outputs['mergeFromBranch'] | Should -Be 'main'
        $result.Outputs['mergeToBranchOverride'] | Should -Be ''
    }

    It 'does not validate the triggering branch, so the inferred value keeps its historical behaviour' {
        $result = Invoke-ResolveBranches -MergeFromInput '' -MergeToInput '' -RefName 'branch/that/does/not/exist'

        $result.ExitCode | Should -Be 0
        $result.Outputs['mergeFromBranch'] | Should -Be 'branch/that/does/not/exist'
    }

    It 'prefers the supplied source branch' {
        $result = Invoke-ResolveBranches -MergeFromInput 'release/9.0.1xx' -MergeToInput '' -RefName 'main'

        $result.ExitCode | Should -Be 0
        $result.Outputs['mergeFromBranch'] | Should -Be 'release/9.0.1xx'
    }

    It 'trims and reports the supplied target branch' {
        $result = Invoke-ResolveBranches -MergeFromInput '' -MergeToInput '  release/override  ' -RefName 'main'

        $result.ExitCode | Should -Be 0
        $result.Outputs['mergeToBranchOverride'] | Should -Be 'release/override'
    }

    It 'treats a whitespace only input as not supplied' {
        $result = Invoke-ResolveBranches -MergeFromInput '   ' -MergeToInput '   ' -RefName 'main'

        $result.ExitCode | Should -Be 0
        $result.Outputs['mergeFromBranch'] | Should -Be 'main'
        $result.Outputs['mergeToBranchOverride'] | Should -Be ''
    }

    It 'resolves both branches independently' {
        $result = Invoke-ResolveBranches -MergeFromInput 'release/9.0.1xx' -MergeToInput 'release/override' -RefName 'main'

        $result.ExitCode | Should -Be 0
        $result.Outputs['mergeFromBranch'] | Should -Be 'release/9.0.1xx'
        $result.Outputs['mergeToBranchOverride'] | Should -Be 'release/override'
    }

    # Each case asserts the reason it was rejected, not merely that something failed. Several of
    # these values would also upset git on their own, so without pinning the reason the allow-list
    # could be deleted while the tests stayed green.
    It 'rejects <Description>' -ForEach @(
        @{ Description = 'a leading dash that git would read as an option'; Value = '-main'; Reason = 'is not an accepted branch name' }
        @{ Description = 'an option that would otherwise reach git'; Value = '--upload-pack=echo'; Reason = 'is not an accepted branch name' }
        @{ Description = 'a command separator'; Value = 'main; whoami'; Reason = 'is not an accepted branch name' }
        @{ Description = 'a subexpression'; Value = 'main$(whoami)'; Reason = 'is not an accepted branch name' }
        @{ Description = 'a leading slash'; Value = '/main'; Reason = 'is not an accepted branch name' }
        @{ Description = 'a trailing slash'; Value = 'main/'; Reason = 'is not an accepted branch name' }
        @{ Description = 'an empty path segment'; Value = 'ma//in'; Reason = 'is not an accepted branch name' }
        @{ Description = 'path traversal'; Value = '../../etc/passwd'; Reason = "must not contain '\.\.'" }
        @{ Description = 'a double dot range'; Value = 'main..release/next'; Reason = "must not contain '\.\.'" }
        @{ Description = 'an unreasonably long name'; Value = ('a' * 300); Reason = 'longer than the 255 character limit' }
        @{ Description = 'a name git itself considers invalid'; Value = 'main.lock'; Reason = 'is not a valid git branch name' }
        @{ Description = 'a branch that does not exist on origin'; Value = 'no/such/branch'; Reason = 'does not exist on the origin remote' }
    ) {
        $fromResult = Invoke-ResolveBranches -MergeFromInput $Value -MergeToInput '' -RefName 'main'
        $fromResult.ExitCode | Should -Not -Be 0
        $fromResult.ErrorMessage | Should -Match $Reason
        $fromResult.ErrorMessage | Should -Match 'merge_from_branch'

        $toResult = Invoke-ResolveBranches -MergeFromInput '' -MergeToInput $Value -RefName 'main'
        $toResult.ExitCode | Should -Not -Be 0
        $toResult.ErrorMessage | Should -Match $Reason
        $toResult.ErrorMessage | Should -Match 'merge_to_branch'
    }
}

Describe 'Resolve merge target step' {
    It 'prefers the supplied target over the configured one' {
        $result = Invoke-ResolveTarget -Override 'release/override' -ConfigurationOutputs @{
            'mergeToBranch'      = 'release/next'
            'configurationFound' = 'True'
            'policyFound'        = 'True'
        }

        $result.Outputs['mergeToBranch'] | Should -Be 'release/override'
        $result.Outputs['configurationFound'] | Should -Be 'true'
    }

    It 'falls back to the configured target' {
        $result = Invoke-ResolveTarget -Override '' -ConfigurationOutputs @{
            'mergeToBranch'      = 'release/next'
            'configurationFound' = 'True'
            'policyFound'        = 'True'
        }

        $result.Outputs['mergeToBranch'] | Should -Be 'release/next'
    }

    # An older 'script_version' emits 'configurationFound' but never 'policyFound'.
    It 'accepts configurationFound on its own, so an older script_version keeps working' {
        $result = Invoke-ResolveTarget -Override '' -ConfigurationOutputs @{
            'mergeToBranch'      = 'release/next'
            'configurationFound' = 'True'
        }

        $result.Outputs['configurationFound'] | Should -Be 'true'
        $result.Outputs['mergeToBranch'] | Should -Be 'release/next'
    }

    It 'accepts policyFound on its own' {
        $result = Invoke-ResolveTarget -Override 'release/override' -ConfigurationOutputs @{ 'policyFound' = 'True' }

        $result.Outputs['configurationFound'] | Should -Be 'true'
        $result.Outputs['mergeToBranch'] | Should -Be 'release/override'
    }

    It 'reports no configuration when neither output was emitted' {
        $result = Invoke-ResolveTarget -Override 'release/override' -ConfigurationOutputs @{}

        $result.Outputs['configurationFound'] | Should -Be 'false'
    }
}

Describe 'Runtime input handling' {
    # Scope: the runtime workflow inputs ('merge_from_branch', 'merge_to_branch',
    # 'configuration_file_branch', 'configuration_file_path') and the source and target branches
    # resolved from them. A workflow expression is substituted into the script source before
    # PowerShell ever parses it, so these values must arrive through step 'env:' entries instead.
    # Reverting that is value identical, so no behavioural test would notice; assert the shape.
    #
    # Deliberately out of scope: 'mergeSwitchArguments' and 'resetToTargetPaths' are derived from
    # the repository's own merge flow configuration file, and the existing design treats that file
    # as trusted. 'ExtraSwitches' in particular is splatted onto the command line by design, which
    # predates this suite and is unchanged by it.
    It 'never interpolates a runtime input or a resolved branch into the <_> run body' -ForEach @(
        'Resolve merge branches'
        'Extract configuration values'
        'Resolve merge target'
        'Merge branches'
    ) {
        $run = Get-WorkflowStepRun $_

        $run.Text | Should -Not -Match '\$\{\{[^}]*inputs\.'
        $run.Text | Should -Not -Match '\$\{\{[^}]*steps\.resolve-branches\.'
        $run.Text | Should -Not -Match '\$\{\{[^}]*steps\.resolve-merge-target\.'
        $run.Text | Should -Not -Match '\$\{\{[^}]*github\.event'
    }

    It 'passes the runtime inputs to the scripts through step environment variables' {
        $environment = Get-WorkflowStepEnvironment 'Resolve merge branches'

        $environment['INPUT_MERGE_FROM_BRANCH'] | Should -Be '${{ inputs.merge_from_branch }}'
        $environment['INPUT_MERGE_TO_BRANCH'] | Should -Be '${{ inputs.merge_to_branch }}'

        $environment = Get-WorkflowStepEnvironment 'Extract configuration values'

        $environment['CONFIGURATION_FILE_BRANCH'] | Should -Be '${{ inputs.configuration_file_branch }}'
        $environment['CONFIGURATION_FILE_PATH'] | Should -Be '${{ inputs.configuration_file_path }}'
    }

    It 'passes the resolved source and target branches to the merge through step environment variables' {
        $environment = Get-WorkflowStepEnvironment 'Merge branches'

        $environment['MERGE_FROM_BRANCH'] | Should -Be '${{ steps.resolve-branches.outputs.mergeFromBranch }}'
        $environment['MERGE_TO_BRANCH'] | Should -Be '${{ steps.resolve-merge-target.outputs.mergeToBranch }}'

        $environment = Get-WorkflowStepEnvironment 'Extract configuration values'

        $environment['MERGE_FROM_BRANCH'] | Should -Be '${{ steps.resolve-branches.outputs.mergeFromBranch }}'
    }
}

Describe 'Inter-branch merge flow' {
    It 'merges into the configured target when neither input is supplied' {
        $flow = Invoke-MergeFlow -MergeFromInput '' -MergeToInput '' -RefName 'main'

        $flow.ResolvedFromBranch | Should -Be 'main'
        $flow.ResolvedToBranch | Should -Be 'release/next'
        $flow.Merges | Should -BeTrue
        $flow.ReportsMissingConfig | Should -BeFalse
        $flow.ReportsMissingTarget | Should -BeFalse
        $flow.Invocation['invokedMergeFromBranch'] | Should -Be 'main'
        $flow.Invocation['invokedMergeToBranch'] | Should -Be 'release/next'
    }

    It 'looks the configuration up by the supplied source branch and merges from it' {
        $flow = Invoke-MergeFlow -MergeFromInput 'release/9.0.1xx' -MergeToInput '' -RefName 'main'

        $flow.ResolvedFromBranch | Should -Be 'release/9.0.1xx'
        $flow.ResolvedToBranch | Should -Be 'main'
        $flow.ConfigurationOutputs['mergeSwitchArguments'] | Should -Be '-AllowAutomatedCommits'
        $flow.Merges | Should -BeTrue
        # The same resolved source must reach the merge, not just the configuration lookup.
        $flow.Invocation['invokedMergeFromBranch'] | Should -Be 'release/9.0.1xx'
        $flow.Invocation['invokedMergeToBranch'] | Should -Be 'main'
        $flow.Invocation['invokedAllowAutomatedCommits'] | Should -Be 'True'
        $flow.Invocation['invokedQuietComments'] | Should -Be 'False'
    }

    It 'merges into the supplied target while keeping the configured policy' {
        $flow = Invoke-MergeFlow -MergeFromInput '' -MergeToInput 'release/override' -RefName 'main'

        $flow.ResolvedFromBranch | Should -Be 'main'
        $flow.ResolvedToBranch | Should -Be 'release/override'
        $flow.Merges | Should -BeTrue
        $flow.Invocation['invokedMergeFromBranch'] | Should -Be 'main'
        $flow.Invocation['invokedMergeToBranch'] | Should -Be 'release/override'
        $flow.Invocation['invokedResetToTargetPaths'] | Should -Be 'eng/Version.Details.xml;global.json'
        $flow.Invocation['invokedQuietComments'] | Should -Be 'True'
    }

    It 'honours both inputs at once' {
        $flow = Invoke-MergeFlow -MergeFromInput 'release/9.0.1xx' -MergeToInput 'release/override' -RefName 'main'

        $flow.ResolvedFromBranch | Should -Be 'release/9.0.1xx'
        $flow.ResolvedToBranch | Should -Be 'release/override'
        $flow.Merges | Should -BeTrue
        $flow.Invocation['invokedMergeFromBranch'] | Should -Be 'release/9.0.1xx'
        $flow.Invocation['invokedMergeToBranch'] | Should -Be 'release/override'
        $flow.Invocation['invokedAllowAutomatedCommits'] | Should -Be 'True'
    }

    It 'merges an entry without MergeToBranch when the target is supplied, keeping its policy' {
        $flow = Invoke-MergeFlow -MergeFromInput 'policy-only' -MergeToInput 'release/override' -RefName 'main'

        $flow.ResolvedFromBranch | Should -Be 'policy-only'
        $flow.ResolvedToBranch | Should -Be 'release/override'
        $flow.Merges | Should -BeTrue
        $flow.Invocation['invokedMergeFromBranch'] | Should -Be 'policy-only'
        $flow.Invocation['invokedMergeToBranch'] | Should -Be 'release/override'
        $flow.Invocation['invokedResetToTargetPaths'] | Should -Be 'eng/Version.Details.xml'
        $flow.Invocation['invokedAllowAutomatedCommits'] | Should -Be 'True'
        $flow.Invocation['invokedQuietComments'] | Should -Be 'True'
    }

    It 'passes the repository through to the merge unchanged' {
        $flow = Invoke-MergeFlow -MergeFromInput '' -MergeToInput '' -RefName 'main'

        $flow.Invocation['invokedRepoOwner'] | Should -Be 'dotnet'
        $flow.Invocation['invokedRepoName'] | Should -Be 'test-repo'
        # The same owner and repository must also reach the configuration lookup.
        $flow.ConfigurationUris[0] | Should -Be $script:ExpectedConfigurationUri
    }

    It 'skips an entry without MergeToBranch when no target is supplied' {
        $flow = Invoke-MergeFlow -MergeFromInput 'policy-only' -MergeToInput '' -RefName 'main'

        $flow.ResolvedToBranch | Should -Be ''
        $flow.Merges | Should -BeFalse
        $flow.ReportsMissingTarget | Should -BeTrue
        $flow.Invocation | Should -Be $null
    }

    # A supplied target must not become a way to merge branches that carry no configured policy.
    It 'skips when there is no configuration entry, even with a supplied target' {
        $flow = Invoke-MergeFlow -MergeFromInput 'release/next' -MergeToInput 'release/override' -RefName 'main'

        $flow.Merges | Should -BeFalse
        $flow.ReportsMissingConfig | Should -BeTrue
        $flow.Invocation | Should -Be $null
    }
}
