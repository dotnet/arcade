# Automated Image Generation

### Summary

*Motivation*: Currently, we have to ask DDFUN to generate base images for our Windows queues when we want to update VS versions or include newest Windows patches. This process has several drawbacks from depending on someone to be in the office and to have time to introducing human error in the process. Additionally this saves couple of hours from FR every week.


*Goal*: The goal of this epic is to automate process of image creation. Mainly:
* Extend osob-cli with functionality to:
    * get state of image creation from the Image Factory based on a Tracking Id
    * submit a new job to Image Factory using payload from a specified file
    * search all configurations which use specified variables, substitute variables and send all payloads to the Image Factory.
* Create a new pipeline which will be triggered by a change of variables files. When any variable is updated (e.g. Visual Studio URL is updated to a new version) then all affected images are regenerated. As image generation takes long time to complete, this pipeline has to be executed several times per day to check status of image creation and to fail on any error.
* Move Helix image definitions from the Image Factory repository to our repository. I checked with Casey that there wasn't ever need for a change, except bounce of VS version. It shouldn't give us additional maintenance cost. The reason why we need to move them is introduction of custom variables (see an example below). As part of the move I would suggest to start using JSON file format as it's expected input of the Image Factory. Having it in YAML would only mean that we will need to transform a payload before sending it.
For more information you can find current image definitions [here](https://devdiv.visualstudio.com/XlabImageFactory/_git/ImageConfigurations?path=%2FMonthly%2FHelixBaseImages)

### Examples

* Variable in the payload:
```
VSBootstrapperURL: "{VS_2019_PREVIEW_URL}/vs_Enterprise.exe"
```
* Variable definitions used by tool:
```
{
    "VS_2019_PREVIEW_URL":"https://aka.ms/vs/16/pre/184776537_-638374648",
    "VS_2019_PREVIEW_VERSION":"16_11_0_4"
}
```

### Proposition for extension of this epic

After images are successfully built raise a new OSOB PR updating image definitions with latest images. This shouldn't take longer than 1 more week of development and would save us lots of time every week.


### Stakeholders

- .NET Engineering Services

### Risk

- Unknow is a way how we patch Windows images
- POC for Visual Studio images was done before this one pager.
- This epic depends on external component Image Factory. It's functionality is currently stable and there aren't planned breaking changes .
- Implementation of this shouldn't cause breaking changes as all issues with images should be detected during OSOB validation phase.


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
