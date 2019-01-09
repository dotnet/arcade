$token = $env:SYSTEM_ACCESSTOKEN
$collectionUri = $env:SYSTEM_TEAMFOUNDATIONCOLLECTIONURI
$teamProject = $env:SYSTEM_TEAMPROJECT
$buildId = $env:BUILD_BUILDID

$headers = @{
  Authorization="Basic " + [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes("unused:$token"));
  'User-Agent'='HelixSdk';
}

$body = @{
  automated=$true;
  build=@{
    id=$buildId;
  };
  name="TestTestRun";
  state="InProgress";
} | ConvertTo-Json
Write-Output $body
$run = Invoke-RestMethod "$collectionUri$teamProject/_apis/test/runs?api-version=5.0-preview.2" -Method Post -ContentType 'application/json' -Body $body -Verbose -Headers $headers
Write-Output $run

$runId = $run.id



$body = @(
  @{
    testCaseTitle="TestTest";
    automatedTestName="TestAssembly.TestTest";
    priority=1;
    outcome="Failed";
    state="Completed";
    errorMessage="An Error Occured";
    stackTrace="This is a stack trace";
  },
  @{
    testCaseTitle="TestTestTest";
    automatedTestName="TestAssembly.TestTestTest";
    priority=1;
    outcome="Passed";
    state="Completed";
  }
) | ConvertTo-Json
Write-Output $body
Invoke-RestMethod "$collectionUri$teamProject/_apis/test/runs/$runId/results?api-version=5.0-preview.5" -Method Post -ContentType 'application/json' -Body $body -Verbose -Headers $headers




$body = @{
  state="Completed";
} | ConvertTo-Json
Write-Output $body
Invoke-RestMethod "$collectionUri$teamProject/_apis/test/runs/$($runId)?api-version=5.0-preview.2" -Method Patch -ContentType 'application/json' -Body $body -Verbose -Headers $headers
