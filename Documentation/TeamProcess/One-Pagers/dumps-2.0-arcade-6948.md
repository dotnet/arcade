# Dump 2.0

## Current process: 
We collect system crash dumps across all platforms. Then we upload the dumps to blob storage and provide link to the repo owners to download in their builds.

## Problems with the current process?
1.	Dumps for hanging test and crashing test are not captured. Repo owners do not have a way to get this data for their crashing/ hanging tests for further investigation.
1.	Dumps are not auto compressed. For workitems that on run on macs and on-prem machines, crash dumps are really huge like 6gb and uploading them to azure storage takes forever or fails. Its hard to debug these scenariors without any logs.
1.	We do not extract any information from dumps, we just upload the dumps to azure storage.It will be good to extract data out of these dumps, so that we can get the actual exception, this will make debugging of system dumps easier.

## Motivation for the new solution? 
1.	We already get system dumps, this feature will let us capture runtime dumps too. This will help repo owners to debug tests for hanging/crashing test. This is one of the most requested features by repo owners, since they do not have a way to debug crashing/hung tests.
1.	We will have the ability to check, which test actually failed.
1.	We can analyze dumps and extract exceptions out of it, to debug system dumps easily.

## Implementation:
1. Currently after helix work item is completed, we kill the process.
Just before we kill the process we do the following 
* If the workitem is hung or taking a long time to complete then we create a dump. 
* Zip the dumps and upload it to azure storage.
* Delete the dump folder after we upload the files.

Coreclr has implemented something similar, so we have proof of concept that this [works](https://github.com/dotnet/runtime/blob/543bcc5ee7d6a2b9471b016770227421c43a756e/src/tests/Common/Coreclr.TestWrapper/CoreclrTestWrapperLib.cs#L207-L254). We can do something similar in helix.

Zipping the contents of the dump folder before uploading it to the storage account. This is especially helpful for Mac OS and on-prem machines, since most of the times dump folder size is more than 6gb, and it takes forever/fails to upload dump files.

We will have to test this across all platforms. 

2.	VS test runner is integrated with our test infra, this should give us the ability to play more with dump management. More details on how we are planning to implement VStest runner is (here) [./shared-test-infra-arcade6948.md]. 

Vs test runner will give us the ability to get more details on 
*	The test that crashed/hung 
*	Create dumps for crashing/hung test out of the box we don’t have to do much customization.
*	Create snapshots on crashing/hung test.

This must be tested across all platforms like the previous implementation.

3.	Dump Auto triage tool: 

* This tool we can use to analyze dumps (extract details on exceptions) on the helix agent. 
* Tool looks for work item’s uploaded dump files. If it finds any dumps for the work item, then it downloads the dump file.
* Will use CLRMD/windbg (for windows)/gdb (for non windows) to open the dump file and look for exceptions and get the stack trace. In this we are not sure what exceptions we should look for in the dump files.
* We can then get the stack trace and create github issues/ azdo issues.

## Risk: 
1.	In the first two implementations (with and without vs test runner), we will have to test this across all platforms. We will have to make sure we allot good time for testing across all platforms.

## Open question:
1.	We are unsure what data to look for in dumps during the dump analysis phase.








