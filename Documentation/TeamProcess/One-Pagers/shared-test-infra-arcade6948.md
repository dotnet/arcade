
## Context
Shared infrastructure is intended to be the test-aware component that gives us a more cohesive experience on how Helix runs and manages tests and how it collects, analyzes, and processes dumps.

To achieve this, Helix will run the test and will have a mechanism to receive all the specific test parameters by repository.

### Current flow
1. User creates and sends job to Helix using an [array of parameters](https://github.com/dotnet/arcade/blob/b888df17a4acb65630c1e9ad5e94f22a33b62ab0/src/Microsoft.DotNet.Helix/Sdk/Readme.md#all-possible-options)
1. Arcade sends this workitem to Helix
1. Helix runs this workitem using provided payloads and running defined command
1.  If the folder contains re-run instructions, Helix will use these to determine appropriate behavior.
    1. If a re-run is required, it will run the whole workitem a second time.
1. The workitem completes, and Helix uploads any completed artifacts and dumps to Helix storage and reports test results to AzDO

## Proposed new flow
1. User creates a Helix job, similar to today, but now there is a `test job` option.
    - For a test job:
      - The job should have a `-test` tag.
      - Details for test runs can be added to the repository `shared-infra-configuration.json` file.
      - A `dll` or `.proj` is expected. 
    - It can also follow the traditional path of sending a command. 
1. The job is sent to Helix and Arcade creates a workitem and adds the `shared-infra-configuration.json` file as a correlation payload.
1. If this is a `-test` job, then Helix client interprets this as a test run and Helix will run the tests using the configured runner.
    - If  the folder contains test configuration (`shared-infra-configuration.json` file) use it to:
        - Skip tests
        - Define the data to collect for failing tests
    - If the folder contains re-run instructions, Helix will use these to determine the appropriate behavior.
      - Retry only the test that meets the requirements to be retried, the test will be retried individually, instead of the whole dll
      - Each artifact will be related to the attempt that triggered it, so the correct console logs can be viewed for each result
    - Analyze the result of the test, identifying:
        - Test crashes
        - Test hanging 
1. If not, it will run the command as it does today.
1. The workitem completes, and Helix uploads any completed artifacts, dumps and test logs requested to Helix storage.


## Implementation
### Stage 1
1. **Helix runs tests using the configured runner:** Helix running tests on every configuration, this will be the base step to let us implement future features for the shared infra. To achieve this, we will be using [**VSTest runner**](https://github.com/microsoft/vstest#vstest). The reason behind this choice is that it can run multiple test framework. We will also explore  if xUnit is needed, due to the diversity of configurations.
1. **Helix identifies and reports hanging tests and tests that caused the crash of the run:** With Helix owning the run of the test we will be working towards identifying hanging test, failed test and test that caused a crash and report them as results of the run.
1. **User integration:** Helix will have a test option (`-test`), that will be passed by the user, this will let us know that Helix will be in charge of running those tests. For this, Helix will accept dll and .proj files on the running test option.
  - Along this option a new payload will be included `shared-infra-configuration.json` this file will have parameters to customize the test run. The file will be fulfilled by each repository. If the file is present, it will automatically be sent as part of the workitem payload.
1. **Screenshot of a hung/failed test:** Helix will accept parameters passed on `shared-infra-configuration.json` file to customize how tests are run and what data is collected for failed tests


### Stage 2
1. **Helix retries individual tests, reports the attempts to AzDO, and consolidates the result of the run:** Integrate the test retry functionality and retry tests individually (instead of the whole dll), for this, we will identify which test meets the test retries instructions and using the test filtering option re-run only those tests. Report the test results with the appropriate attempts back to AzDO.
1. **Helix skips tests:** Be able to skip tests, the desired skipped tests will be included on the `shared-infra-configuration.json` file. 
1. **Helix accepts custom downloaders as a configuration for the test run:** Customize which files are gathered while running the test.
1. **Report passing tests to Kusto:** Report tests to Kusto (passing tests), this is with the intention of getting rid of the backchannel


### Stage 3
1. **Automatic detection of changes that cause test failures:** Analyze results of failed tests to decide which commit started the failure. This will be a critical part if we want to pursue skipping some tests per run. This information will be updated in a Kusto table so it can be read from Build Analysis and could create more valuable information with 'Test Reporting' tables. Additionally this information can be included on the website. 

## Proof of Concept (POC)
1. Share configuration file to Helix: We already have, the `test-configuration.json` in the same way that `shared-infra-configuration.json`.
1. VSTest as designated runner:
    - Using VSTest run test, with different filters and capabilities that we are looking for (skip, retry).
    - Send jobs to Helix in queue from Windows/OSX and Linux to validate VSTest can run on these platforms, if this is not the case explore if xUnit would do the job on these scenarios or look for a different runner.
    - Make sure that by using VSTest we can get crashing test info, hanging test info, screenshots, and logs. All this can be done locally to prove that we can gather all this information and how specific it can be.
    - Verify if we can categorize or filter test logs to upload only the requested ones.

## Risk

- All the implications of Helix running tests on multiple platforms and the difficulties with fully testing this. 
- Python code is not fully tested, which puts a lot of weight on breaking something (Helix related) while adding this logic, which could impact users.
- End up creating even bigger dumps as we include hanging tests and instead of reducing costs, increase them.
- Problems with reporting this information to AzDO.

## Usage Telemetry

#### Track usage of new feature
- Track tests that are retried and have necessary information to connect with final result of the build (Answer question is this feature helping (retry tests) to have a green build)
- Create a flaky test metric (possible with test reporting info) to encourage people to use the test retries and observe how many repos are using the feature for this flaky test identified by us. In other words look if the users are using the feature on the expected places.


#### Measuring the “usefulness” to the stakeholders of the business objectives
- Monitor usage of storage blobs, expecting this number to decrease.
- Monitor the download of files from storage
- Ask users about their experience and satisfaction  while investigating test failures. We can have a survey at the beginning, middle and end of a project, looking to see an improvement with all the new features.
- Least time having a broken test. Measure the ratio of failing tests to passing tests and observe behavior and average of time a test is broken.

