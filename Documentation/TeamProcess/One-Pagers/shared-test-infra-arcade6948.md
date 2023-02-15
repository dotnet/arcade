
## Context
Shared infrastructure is intended to be the layer that gives us a more cohesive experience on how Helix runs and manages tests and how it collects, analyzes, and processes dumps.

To achieve this, Helix will control the running of the test and will have an infrastructure to send all the specific test parameters. This same infrastructure is going to be used to share specifics about which dumps to collect.

### Current flow
1. User creates and sends job to Helix using an [array of parameters](https://github.com/dotnet/arcade/blob/b888df17a4acb65630c1e9ad5e94f22a33b62ab0/src/Microsoft.DotNet.Helix/Sdk/Readme.md#all-possible-options)
1. Arcade sends this workitem to Helix
1. Helix runs this workitem using provided payloads and running defined command
1.  If the folder contains re-run instructions, Helix will use these to determine appropriate behavior.
    1. If a re-run is required, it will run the whole workitem a second time.
1. The workitem completes, and Helix uploads any completed artifacts and dumps to Helix storage and reports test results to AzDO

## Design details
### Proposed new flow
1. User creates a Helix job, similar to today, but now there is a `test job` option.
    - For a test job, it  should have a `-test` tag and details can be added to the `shared-infra-configuration.json` file, in this case, a `dll` or `.proj` is expected. 
    - It can also follow the regular path.
1. The job is sent to Helix and Arcade creates a workitem and adds the `shared-infra-configuration.json` file as a payload.
1. If this is a `-test` job, Helix client interprets this as a test run and Helix will run the tests using the VSTest runner or the defined runner
    - Use the provided parameters on `shared-infra-configuration.json`
        - Skip tests
        - Define the data to collect for failing tests
    - If the folder contains re-run instructions, Helix will use these to determine the appropriate behavior.
      - Retry the test that meets the requirements to be retried, the test will be retried individually, instead of the whole dll
      - Each artifact will be related to the attempt that triggered it, so the correct console logs can be viewed for each result
    - Analyze the result of the test, identifying:
        - Test crashes
        - Test hanging 
1. If not, it will run the command as it does today
1. The workitem completes, and Helix uploads any completed artifacts, dumps and test logs requested to Helix storage


### Milestones

**Milestone A**

- Shared tests
  - Helix runs tests using VSTest runner / xUnit 
  - Helix identifies and reports hanging tests and tests that caused the crash of the run
  - Screenshot of a hung/failed test

- Dumps management
  - Have a `final` process for Helix work items to give the opportunity to collect dumps in crashing situations
  - Generate dumps for hanging tests 
  - Generate dumps for tests crashed
  - Automatically compress dumps

**Milestone B**
- Shared tests 
  - Helix skips and retries individual tests, reports the attempts to AzDO, and consolidates the result of the run
  - Helix accepts custom downloaders as a configuration for the test run
  - Report passing tests to Kusto
- Dumps management
  - Dumps for selected test failures (categorized)
  - Categorize dumps

**Milestone C**
- Shared test infrastructure
  - Automatic detection of changes that cause test failures

- Dumps management
  - Helix manage dump auto-triage

### Implementation details

**Stage 1**
#### Shared tests
1. Helix running tests on every distro, this will be the base step to let us implement future features for the shared infra. To achieve this, we will be using [**VSTest runner**](https://github.com/microsoft/vstest#vstest). The reason behind this choice is that it can run multiple test framework.
1. With Helix owning the run of the test we will be working towards identifying hanging test, failed test and test that caused a crash and report them as results of the run.
1. Helix will accept parameter passed on `shared-infra-configuration.json` to customize how test are run, which data is collected for failed tests
1. User integration: Helix will have a test option (`-test`), that will be passed by the user, this will let us know that Helix will be in charge of running those tests. For this, Helix will accept dll and .proj files on the running test option.
  - A long this option a new payload will be included `shared-infra-configuration.json` with custom parameter to run the test which will be also used for the dumps configuration

**Stage 2**
#### Shared tests
1. Integrate the test retry functionality and retry test individually (instead of the whole dll), for this we will identify which test meets the test retries instructions and using the test filtering option re-run only those tests. Report the test results with the appropriate attempts back to AzDO.
1. Being able to skip tests, the desired skipped test will be included on the `shared-infra-configuration.json` file. 
1. Customize which files are gathered while running the test.
1. Report test to Kusto (passing tests), this is with the intention of get rid of the backchannel


**Stage 3**
#### Shared tests
1. Analyze results of failed tests to decide which commit started the failure. This will be a critical part if we want to pursue skipping some test per run. This information will be updated in Kusto table so it can be read from Build Analysis and could create more valuable information with 'Test Reporting' tables. Additional this information can be included on the website. 

### Proof of Concept (POC)
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

