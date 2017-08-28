# AppCompat Runs in Helix

This document describes .NET Native AppCompat run operations in Helix.

- [Run controller](#run-controller)
- [Tools to perform runs](#tools-to-perform-runs)
- [Run data](#run-data)
- [Day-to-day operations](#day-to-day-operations)
- [Troubleshooting guide](#troubleshooting-guide)


## Run controller

Run controller is a dedicated, DDIT managed VM for performing all AppCompat run operations. Details of this VM are as follows -

|Property|Value|
|:--------|:----|
| Name | AC-RC-DDIT |
| Username (admin) | redmond\corbvt |
| Password | Get it from [DevDiv Key vault](https://ms.portal.azure.com/#asset/Microsoft_Azure_KeyVault/Secret/https://appcompat.vault.azure.net/secrets/corbvt/9b8d9b2042c440749b941600031a2ecb) |
| CPUs, Memory, OS | 4, 8 GB, Windows Server 2016 |
| Applications | Visual Studio 2017 |


## Tools to perform runs

All the necessary tools and scripts to perform AppCompat runs through Helix are in CoreFxAppCompat repository (https://devdiv.visualstudio.com/DefaultCollection/DevDiv/_git/CoreFxAppCompat). Clone this to "C:\Users\corbvt\Source\Repos\CoreFxAppCompat" on the run controller. Summary of each tool is shown in the table below.


| Tool | Description |
| :-------- | :---- |
| GetBUTs| Determines the configurations for which a run needs to be performed. This is determined based on the coverage defined in `schedule.json`, run history and availability of latest build. |
| HelixJobCreator | Prepares and submits a Helix job for each configuration listed in the input json. GetBUTs writes the configuration to a json, and invokes HelixJobCreator with input argument as the path to the json. |
| HelixDownloadRunResults | Downloads AppCompat run results from Azure Storage account to the corpnet share. |
| HelixAnalyzeResults | For a finished job, if results are available on the corpnet share then, launches Reporter to analyze the results. |
| Reporter | Analyzes run results, and generates reports. |
| RemoveOldResults | Deletes Azure Storage Containers that hold AppCompat run results. Also deletes the corresponding corpnet folders that were downloaded. Current retention policy is Containers and Folders older than 2-weeks are deleted.  |
| AppUpload |  Uploads apps listed in AppLists, for example (\\fxcore\apps\WindowsStore\UWP\AppLists\UWP_x86.txt), to Azure Storage. |
| AppCompatHelper | Provides helper methods such as retrieval of Key Vault secrets, which are used by one or more of the above listed tools. |



## Run data

Details of all AppCompat runs are stored in a SQL Azure database. Connection string for the database is - `dotnetappcompatsql.database.windows.net;initial catalog=AppCompat;user id=sql-azure-read-only;password=get_it_from_keyvault;MultipleActiveResultSets=True;App=EntityFramework`

Get password from the Key Vault at (https://ms.portal.azure.com/#asset/Microsoft_Azure_KeyVault/Secret/https://appcompat.vault.azure.net/secrets/sql-azure-read-only/1c32ad5ff6ac46b9892c4a624c691d19)

| Table | Description |
|:------|:------------|
| Helix_AppCompatRuns | Detailed configuration of each run submitted. |
| Helix_AppCompatResults | Result of each workitem (test or app). |
| Helix_ReporterResults | Result of each workitem post-Reporter processing. |


Helix uploads run results as blobs to AppCompat Storage account at (https://ms.portal.azure.com/#resource/subscriptions/9c035fa3-535f-4bf9-a60a-1381e6d27ea5/resourceGroups/dotnetappcompat/providers/Microsoft.Storage/storageAccounts/dotnetappcompat/overview).  DDIT manages access to this account.


## Day-to-day operations

AppCompat runs in Helix are fully automated. This means as new builds are produced, corresponding AppCompat run will be performed. Based on the defined coverage schedule, Helix jobs are prepared, submitted, monitored, results analyzed and reports sent to the product owners. If a piece of this automation breaks, for example, a symptom would be report emails not received, then login to the run controller (AC-RC-DDIT),  and verify if the following 4 tools are running smoothly -

 - GetBUTs
 - HelixDownloadRunResults
 - HelixAnalyzeResults
 - RemoveOldResults


## Troubleshooting guide

#### How to setup a new run controller

 1. Setup a VM with configuration described in [Run controller](#run-controller)
 2. Clone https://devdiv.visualstudio.com/DefaultCollection/DevDiv/_git/CoreFxAppCompat 
 3. Launch a developer command prompt in administrator mode
 4. Rebuild all tools by running the command `Tools\Rebuild\RebuildTool.bat`. Ensure there are no package restore or build errors.
 5. Launch tools by running the command `Tools\Launch.bat`


#### How to add or remove a configuration from coverage

Coverage is defined in `schedule.json` (Tools/GetBUTs/GetBUTs/schedule.json). Each object in the json should have the following attribute-value pairs -
| Attribute | Description |
|:----------|:------------|
| DayOfWeek | Day of the week for which this config applies. Values will be `Mon-Thu` or {`Friday`, `Saturday`, `Sunday`} |
| Config# | Configuration number. Unique for a given day of the week. | 
| Branch | Name of the branch | 
| Architecture | {`x86`, `amd64`, `arm`} | 
| Flavor | {`chk`, `ret`} | 
| UseSharedAssembly | TRUE if multi-file otherwise FALSE. | 
| IsTP | TRUE if targeted patching otherwise FALSE. | 
| AppCount | Number of UWP apps to include in the run. | 
| IsBaseline | TRUE if baseline run otherwise FALSE. | 

A sample config is shown below. 
    {
        "DayOfWeek":  "Mon-Thu",
        "Config#":  "1",
        "Branch":  "ProjectN",
        "Architecture":  "x86",
        "Flavor":  "ret",
        "UseSharedAssembly":  "TRUE",
        "IsTP":  "FALSE",
        "AppCount":  "2000",
        "IsBaseline":  "FALSE"
    },

Update the `schedule.json` to add, update or delete an object as required for the new coverage. Make sure to run `RebuildAll.bat` for the new changes to be applied.


#### How to check the status of Helix jobs

AppCompat jobs to Helix are submitted using `corefxac` token.  Status of these jobs is available at https://mc.dot.net/#/user/corfxac/builds


For any questions about AppCompat, please contact [corefxappcompat](corefxappcompat@microsoft.com).
