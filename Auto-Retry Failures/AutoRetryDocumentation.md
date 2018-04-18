#  Documentation for Auto Retry of Failures in Helix 
The following document shows the user documentation of the auto retry feature of test failures in helix according to the [requirements](https://github.com/dotnet/core-eng/blob/master/Documentation/Project-Docs/Auto-Retry%20Failures/Overview-Requirements.md).

# What is Auto Retry?
When a Test fails in Helix, Auto Retry feature provides the ability to automatically retry the test until it passes or crosses the number of retries specified by the user and communicate to the user that the test has passed only after retrying in Mission Control.

# How to configure Auto Retry?
By adding the optional property (MaxRetryCount) to the MSBuild definition of a repo is all that is needed to be done to turn Auto Retry on. 
For eg. 
For CoreFx, Add `<MaxRetryCount> (Value of allowable retries) </MaxRetryCount>` under the `<PropertyGroup>` in https://github.com/dotnet/corefx/blob/master/src/upload-tests.proj#L11-L79. Once the build picks up the optional property, tests will automatically requeue failed work items to be retried until the MaxRetryCount is reached / the work item passes.

# Mission Control
In order to see the information in [Mission Control], the user must **Log in**, otherwhise no information will be available.

If a work item passed on a retry, the information will be displayed by default along with the count of items that had passed on retry.
![](./Images/WorkItemAggregateSummary_Count.JPG?raw=true)

Each Work Item that was passed on retry will display with a retry icon and tool tip showing `Intermittent Failures`
![](./Images/WorkItemAggregateSummary_Icon.JPG?raw=true)

Each attempt of retry details can be viewed from the Tests Details Page.
![](./Images/Logs.JPG?raw=true)

# How to give feedback?
Please create a new issue in the [core-eng](https://github.com/dotnet/core-eng) repository.

[Mission Control]: https://mc.dot.net/#/