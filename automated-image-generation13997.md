# Automated Image Generation

### Summary

*Motivation*: Currently, we have to ask DDFUN to generate base images for our Windows queues when we want to update VS versions or include newest Windows patches. This process has several drawbacks from depending on someone to be in the office and to have time to introducing human error in the process.


*Goal*: The goal of this epic is to automate process of image creation. Mainly:
* Command line tool wich:
    * gets state of image based on tracking id
    * submits a new job to Image Factory using payload from specified file
    * given variables, searches all configurations which use these variables and send all of them to the Image Factory (additional it would be great to raise a new OSOB PR with changed images, but I'm not sure if this fits into this short epic)
* Image definitions should be stored on our side instead of Image Factory repository - these have to be updated as we need to introduce variables.

Examples
* Used variable in configuration file:
```
VSBootstrapperURL: "{VS_2019_PREVIEW_URL}/vs_Enterprise.exe"
```
* Tool configuration:
```
{
    "VS_2019_PREVIEW_URL":"https://aka.ms/vs/16/pre/184776537_-638374648",
    "VS_2019_PREVIEW_VERSION":"16_11_0_4"
}
```

### Stakeholders

- .NET Engineering Services

### Risk

- Unknow is a way how we patch Windows images
- POC for Visual Studio images was done before this one pager.
- This epic depends on external component Image Factory. It's functionality is currently stable and there aren't planned breaking changes .
- Implementation of this shouldn't cause breaking changes as all issues with images should be detected during OSOB validation phase.
- We have to do this manually if this isn't implemented and manual steps always open space for errors. Additionally this saves couple of hours from FR every week.

### Serviceability

- Tested by scenario tests.
- Executed several times per week and we will spot any issue immediately.
- Existing secrets:
	* image-factory-tenant-id
	* image-factory-client-id
	* image-factory-client-secret
	* image-factory-resource-id
- It doesn't change SDL threat or data privacy models.
- No repro/test/dev environments. Image starts to be used in production once updated in OSOB configuration and this change is deployed into production. Then it builds a new image based on an image from the Image Factory.

#### Rollout and Deployment
- Pipeline will take this tool from artifacts and executes it - once the change is merged it will be automatically used on the next execution.
- It is expected to be updated rarely.
- There is no risk as we can always build images manually if something goes wrong.
- It is dependent on the Image Factory. Breaking change in API of the Image Factory will break this tool too.

### Usage Telemetry
- No usage telemetry as this tool isn't exposed to customer.

### Monitoring
- Only monitoring is done by pipeline where we will see failure.

### FR Hand off
- Part of this epic will be documentation for FR containing how to use the command line tool and how to detect failures on the pipeline.
