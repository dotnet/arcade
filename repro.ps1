$token = $env:SYSTEM_ACCESSTOKEN
$collectionUri = $env:SYSTEM_TEAMFOUNDATIONCOLLECTIONURI
$teamProject = $env:SYSTEM_TEAMPROJECT
$buildId = $env:BUILD_BUILDID

$heaers = @{
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
$run = Invoke-RestMethod -Verbose -Headers $headers -Method Post -ContentType 'application/json' -Uri "$collectionUri$teamProject/_apis/test/runs?api-version=5.0-preview.2" -Body $body

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
  }
) | ConvertTo-Json
Invoke-RestMethod -Verbose -Headers $headers -Method Patch -ContentType 'application/json' -Uri "$collectionUri$teamProject/_apis/test/runs/$runId/results?api-version=5.0-preview.5" -Body $body




$body = @{
  state="Completed";
} | ConvertTo-Json
Invoke-RestMethod -Verbose -Headers $headers -Method Patch -ContentType 'application/json' -Uri "$collectionUri$teamProject/_apis/test/runs/$runId?api-version=5.0-preview.2" -Body $body
