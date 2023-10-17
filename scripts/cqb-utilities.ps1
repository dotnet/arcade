# Set of utility functionality for coherency QBs

Function Gen-Merge-Branch
{
  param (
      [string]$BranchName
  )

  & git fetch --all -p
  & git checkout internal/release/$BranchName
  if (-not $?) {
    return 1
  }
  & git merge azdo-dnceng-internal/internal/release/$BranchName
  if (-not $?) {
    return 1
  }
  & git checkout -B merge-internal/release/$BranchName
  if (-not $?) {
    return 1
  }
  & git merge upstream/release/$BranchName
}

Function Gen-Internal-Merge-PR
{
  param (
      [string]$RepoName,
      [string]$Org = "dnceng",
      [string]$Project = "internal",
      [string]$BranchName,
      [string]$AzDOPAT
  )

  $header = Get-AzDO-Auth-Header $AzDOPAT
  
  if ($BranchName -contains "/") {
    Write-Error "Branch name should just be the last segment of the branch name path"
    return
  }
  
  $prBody = @{
    sourceRefName = "refs/heads/release/$BranchName";
    targetRefName = "refs/heads/internal/release/$BranchName";
    title = "[internal/release/$BranchName] Merge from public";
    description = "Merge from public release/$BranchName to internal/release/$BranchName and resolve conflicts if necessary"
  }
  
  try {
    $resp = Invoke-WebRequest -Method Post -Body $($prBody | ConvertTo-Json) -Headers $header -Uri "https://dev.azure.com/$Org/$Project/_apis/git/repositories/$RepoName/pullrequests?api-version=7.0" -ContentType application/json
    $result = $resp | ConvertFrom-Json
    Write-Host https://dev.azure.com/$Org/$Project/_git/$RepoName/pullrequest/$($result.pullRequestId)
  } catch {
    Write-Error $_
  }
}

Function Get-AzDO-Auth-Header
{
  param (
      [string]$AzDOPAT
  )
  
  $base64authinfo = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes(":$AzDOPat"))
  return @{"Authorization"="Basic $base64authinfo"}
}

Function Mark-All-In-Manifest-As-Released
{
  param (
      [string]$Manifest
  )

  $manifestContent = Get-Content $Manifest | ConvertFrom-Json
  foreach ($build in $manifestContent.builds) {
    Write-Host "Marking $($build.repo) @ $($build.commit) (BAR ID: $($build.barBuildId)) as released"
    darc update-build --released --id $build.barBuildId
  }
}