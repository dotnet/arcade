# Dump 2.0

## Current process: 
We collect system crash dumps across all platforms. Then we upload the dumps to blob storage and provide link to the repo owners to download in their builds.
Problems with the current process?
1.	Dumps for hanging test and crashing test are not captured. 
1.	Dumps are not auto compressed.
1.	We cannot analyze dumps.
Motivation for the new solution? 
1.	Get system and runtime dumps.
1.	We can work on the ability to know which test hung/crashed. 
1.	We can capture screenshots of hung/crashed tests. 
1.	We can analyze dumps and get the data out of it to categorize failures. 

## Implementation:
1.	For every helix work item, before the we kill the process, we can handle it better by adding some cleanup process and uploading the dumps if they were created when the work item ran. 

We can also gzip the contents of the dump folder before uploading it to the storage account. This is especially helpful for Mac OS, since most of the times dump folder size is more than 4gb, that way we don’t lose the contents of the dump folder. 

Coreclr has implemented something similar, so we have proof of concept that this works.  

We will have to test this across all platforms and that is a time-consuming process.

1.	VS test runner is integrated with our test infra, this should give us the ability to play more with dump management. More details on VStest runner. 

Vs test runner will give us the ability to get more details on 
*	The test that crashed/hung 
*	Create dumps for crashing/hung test out of the box we don’t have to do much customization.
*	Create snapshots on crashing/hung test.

This must be tested across all platforms like the previous implementation.

1.	Dump Auto triage tool: (nice to have feature) 

*	Tool looks for work item’s uploaded dump files. If it finds any dumps then it downloads the dump file. 
*	Will use CLRMD to open the dump file and look for exceptions and get the stack trace. (Not sure how this will work)
*	This does not account for await and async methods. (Not sure how that will work)
*	We can then get the stack trace and create github issues/ azdo issues.

## Risk: 
1.	If we use the first implementation, we will have to test this across all platforms. This has historically been proven to be time-consuming. 
1.	After the VS test runner implementation, we will need to do the same tests across all platforms. 
1.	We are unsure what data to look for in dumps during the dump analysis phase. 








