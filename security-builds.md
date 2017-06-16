# Security Builds for .NET Core

This document describes how security builds are setup for .NET Core.

Security Development Lifecycle ([SDL](http://sdl/)) specifies the minimum security requirements that must be satisfied before making a Microsoft software or service available to customers. To help product teams fulfill the security requirements, SDL team provides a few tools and services, in addition to detailed guidance and a dedicated support team. Some these tools and services are available as a VSTS extension called Secure Development Tools ([SDT](https://www.1eswiki.com/wiki/Secure_Development_Tools_VSTS_Extension)), which is a collection of build tasks. These build tasks can be added to a VSTS build definition. 

Trust Services Automation ([TSA](http://sql/wiki/Trust_Services_Automation_%28TSA%29)) is a service that analyzes the logs produced for security tools, identifies regressions, creates workitems to track the regressions, and generates a detailed report. One of the tasks in SDT extension is to collect logs from security tools and upload them for processing at TSA. This allows product teams to setup a VSTS build definition that acquire the latest version of security tools, run the tools against the product, gather and analyze logs, detect regressions, and prepare reports.

Security build for .NET Core is a VSTS build definition that uses SDT extension. A security build does not involve building the product from source. This build operates on build artifacts of an official build.  The approach for security build can be summarized as follows.

 1. Download the packages, using `sync` command, for the specified official build Id
 2. Extract assemblies and symbols from the packages
 3. Run security tasks that scan assemblies. Use APIScan and BinSkim tasks from SDT extension.
 4. Get the sources at the SHA specified in `version.txt`, which is obtained when packages are extracted at step #2
 5. Run security tasks that scan source code. Use CredScan and PoliCheck tasks from SDT extension.
 6. Gather logs and upload to TSA. Use publish task in SDT extension.

SDT extension currently support 4 tools that are applicable to .NET Core. A short description of each tool is shown in the table below.

|Tool|Description|
|:---|:----------|
|BinSkim | Validates compiler/linker settings and other security-relevant binary characteristics.|
|APIScan | Determines whether or not the software complies with the API Usage Standard of the Interoperability Policy.|
|CredScan | Index and scan for credentials or other sensitive content.|
|PoliCheck | Scan code, code comments, and content for words that may be sensitive for legal, cultural, or geopolitical reasons.|

.NET Core security build definitions and link to the report is listed in the table below.

|Build Definition|TSA Report|
|:---------------|:---------|
|[CoreFx](https://devdiv.visualstudio.com/DefaultCollection/DevDiv/_build/index?context=Mine&path=%5CDotNet%5Craeda&definitionId=6552&_a=completed)|[CoreFx-master](http://aztsa/api/Result/CodeBase/DotNet-CoreFx-Trusted_master/Summary)|
|[CoreCLR](https://devdiv.visualstudio.com/DefaultCollection/DevDiv/_build/index?context=Mine&path=%5CDotNet%5Craeda&definitionId=6598&_a=completed)|[CoreCLR-master](http://aztsa/api/Result/CodeBase/DotNet-CoreCLR-Trusted_master/Summary)|
|Core-Setup|TODO|

In the current setup, a security build is triggered manually. Official Id and corresponding Azure container name  needs to be provided at the time of queuing the build. In near future, Maestro will be extend to determine the Official Id and container name, and trigger a security build automatically.  Issue #[970](https://github.com/dotnet/core-eng/issues/970)

TSA is configured to send an email report for each scan or security build to [dncsec](dncsec@microsoft.com) that include .NET Core repository owners responsible for security issues. Repository owners should focus on new issues and regressions highlighted in the report, and take necessary action to resolve those issues.

For more details on security builds, contact [dncsec](dncsec@microsoft.com).
