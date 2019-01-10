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
    testCaseTitle="TestTest1";
    automatedTestName="TestAssembly.TestTest1";
    priority=1;
    resultGroupType='rerun';
    state="Completed";
    outcome="Passed";
    durationInMs=1250;
    subResults=@(
      @{
        outcome="Failed";
        errorMessage="An Error Occured";
        stackTrace="This is a stack trace";
        displayName="Attempt #1";
        sequenceId=1;
        durationInMs=1250;
      },
      @{
        outcome="Passed";
        displayName="Attempt #2";
        sequenceId=2;
        durationInMs=1250;
      }
    )
  },
  @{
    testCaseTitle="TestTest2";
    automatedTestName="TestAssembly.TestTest2";
    priority=1;
    resultGroupType='rerun';
    state="Completed";
    outcome="Failed";
    errorMessage="An Error Occured";
    stackTrace="This is a stack trace";
    durationInMs=1250;
    subResults=@(
      @{
        outcome="Failed";
        errorMessage="An Error Occured";
        stackTrace="This is a stack trace";
        displayName="Attempt #1";
        sequenceId=1;
        durationInMs=1250;
      },
      @{
        outcome="Failed";
        errorMessage="An Error Occured";
        stackTrace="This is a stack trace";
        displayName="Attempt #2";
        sequenceId=2;
        durationInMs=1250;
      }
    )
  },
  @{
    testCaseTitle="TestTestTest1";
    automatedTestName="TestAssembly.TestTestTest1";
    priority=1;
    outcome="Passed";
    state="Completed";
  },
  @{
    testCaseTitle="TestTestTest2";
    automatedTestName="TestAssembly.TestTestTest2";
    priority=1;
    outcome="Failed";
    state="Completed";
    errorMessage="An Error Occured";
    stackTrace="This is a stack trace";
  }
) | ConvertTo-Json
Write-Output $body
Invoke-RestMethod "$collectionUri$teamProject/_apis/test/runs/$runId/results?api-version=5.0-preview.5" -Method Post -ContentType 'application/json' -Body $body -Verbose -Headers $headers



$body = @{
  state="Completed";
} | ConvertTo-Json
Write-Output $body
Invoke-RestMethod "$collectionUri$teamProject/_apis/test/runs/$($runId)?api-version=5.0-preview.2" -Method Patch -ContentType 'application/json' -Body $body -Verbose -Headers $headers
