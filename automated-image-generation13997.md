# Automated Image Generation

[Epic](https://github.com/dotnet/core-eng/issues/13997)

### Summary

*Motivation*: Currently only way how to generate Helix custom images for our Windows queues is to ask DDFUN. This happens on weekly basis, always when we want to update Visual Studio versions or include newest Windows patches. This process has several drawbacks like allocating someone from other team to work on our tasks and possible introduction of typos.


*Goal*: The goal of this epic is to automate process of generation of Helix custom images in the Image Factory. These are images contain various versions of Visual Studio and various versions of Windows. All existing definitions of custom images can be found [here](https://devdiv.visualstudio.com/XlabImageFactory/_git/ImageConfigurations?path=%2FMonthly%2FHelixBaseImages).
This will be implemented as a new command line tool which submits jobs for generation of new custom images into the Image Factory and which will track their state. This tool will be executed from a new Azure DevOps pipeline and will introduce support of templates in image definitions. For example when a user changes URL of an installation artifact for Visual Studio, the tool will detect all affected images and will automatically regenerate these images.

*Details*:

Documentation of the Image Factory API can be found [here](https://devdiv.visualstudio.com/XlabImageFactory/_wiki/wikis/XlabImageFactory.wiki/6330/AccessingImageFactory).

Here is an example of a fragment of templated image definition:
```
# List of artifacts and parameters. Secrets will be marked by kv_<secretName>
Artifacts:
- Name: Windows-VisualStudio-Bootstrapper
  Parameters:
    WorkLoads: helix-reduced
    Sku: Enterprise
    VSBootstrapperURL: "{VS_2022_URL}/vs_Enterprise.exe"

Destination:
- StorageAccountName: heliximgfctdncwus2
  CustomImageName: "Helix-Server-Datacenter2019-VS2022Enterprise_{VS_2022_VERSION}"
  SubId: 84a65c9a-787d-45da-b10a-3a1cefce8060
```

You can see that we used variables {VS_2022_URL} and {VS_2022_VERSION} instead of having these values hardcoded.

When we need to regenerate images for Visual Studio 2022, we need to update configuration file containing variables and push this change into the Git repository. Example of the configuration file containing variables:
```
{
    "VS_2022_URL":"https://aka.ms/vs/16/pre/184776537_-638374648",
    "VS_2022_VERSION":"17_0_3_1"
}
```

This change then triggers build pipeline which executes the new tool. This tool checks all templates, substitutes variables, detects which images haven't yet been created and submits jobs into the Image Factory. Pipeline will be also triggered automatically several times per day to track status of image creation and to check for errors.

Making image definitions templated requires modifications. This is why we need to move all of [them](https://devdiv.visualstudio.com/XlabImageFactory/_git/ImageConfigurations?path=%2FMonthly%2FHelixBaseImages) under our repository. I checked that there wasn't ever a need for changing them. This shouldn't give us additional maintenance cost. As part of the move I would suggest to start using JSON file format as it's expected input of the Image Factory. Having them in YAML would only mean that we will need to transform payloads before sending them.
### Examples



### Proposition for extension of this epic

After images are successfully built, raise a new OSOB Pull Request updating image definitions with the latest images. This shouldn't take longer than one additional week of development and would save us a lot of time every week.


### Stakeholders

- .NET Engineering Services

### Risk

- Unknow is a way how we patch Windows images
- POC for Visual Studio images was done before this one pager.
- This epic depends on external component Image Factory. It's functionality is currently stable and there no breaking changes are planned.
- Implementation of this shouldn't cause breaking changes as all issues with images should be detected during OSOB validation phase.


### Serviceability

- Tested by unit tests.
- Executed several times per day or when configuration is changed. We will see any issues immediately.
- Existing secrets:
	* image-factory-tenant-id
	* image-factory-client-id
	* image-factory-client-secret
	* image-factory-resource-id
- It doesn't change SDL threat or data privacy models.
- No repro/test/dev environments. Image starts to be used in production once updated in OSOB configuration and this change is deployed into production. Then it builds a new image based on an image from the Image Factory.

#### Rollout and Deployment
- A new pipeline will take this tool from artifacts and executes it - once the change is merged, it will be automatically used on the next execution.
- It isn't expected to be changed unless the contract of the Image Factory is changed. We asked to be notified about breaking changes in advance. In the worst case, we will be notified by failure on the pipeline.
- There is no risk as we can always build images manually if something goes wrong.
- This tool is dependent on the Image Factory. Breaking change in API of the Image Factory will break this tool too.

### Usage Telemetry
- This tool isn't exposed to customer. Basic information about runs will be available from the pipeline history. We don't plan to include any additional data, unless they are requested. If we start experience problems with the image generation though, we might need to start gathering some reliability data.

### Monitoring
- Monitoring is done by result of the build pipeline. It will send email notification on any failure. If there is any issue, it should be picked up by FR.

### FR Hand off
- Part of this epic will be documentation for FR describe how to use the command line tool and how to detect failures on the pipeline.
