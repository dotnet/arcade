# Automated Image Generation

[Link to Automated Image Generation Epic](https://github.com/dotnet/core-eng/issues/13997)

## Summary

### Motivation
Currently only way how to generate Helix custom images for our Windows queues is to ask DDFUN. This happens on a weekly basis, always when we want to update Visual Studio versions or include the newest Windows patches. This process has several drawbacks from allocating someone from DDFUN team to introduction of typos by manual steps.


### Goal
The goal of this epic is to enable our team to be able to generate custom images by ourself and remove the dependency on DDFUN. These are images which contains various versions of Visual Studio and various versions of Windows. We will introduce a new automated and monitored process which removes the need for manual steps done by DDFUN consisting of:
* updating tens of configuration files
* sending them into the Image Factory
* monitoring of completion

Part of this epic is also to take the ownership of custom image definitions from DDFUN as this will enable us to introduce automation and make changes more quickly in future. You can find all current Helix custom image definitions [here](https://devdiv.visualstudio.com/XlabImageFactory/_git/ImageConfigurations?path=%2FMonthly%2FHelixBaseImages).

### Implementation Details

Let's start with a scenario where we need to updated Visual Studio 2019 Preview version per our [schedule](https://github.com/dotnet/core-eng/wiki/VS2019-Upgrade-Schedule) which has to be done almost every week.

This change requires update in six [image definitions](https://devdiv.visualstudio.com/XlabImageFactory/_git/ImageConfigurations?path=/Monthly/HelixBaseImages/VS2019Preview). The change in each file is to:
* update artifact windows-vs-willowreleased, set parameter VSBootstrapperURL to a new value.
* update version in parameter CustomImageName under Destination.

To simplify this, we will introduce templating. Template variables help you to re-use shared data from variables file in any template. For example the template variable {VS_2019_PREVIEW_URL} declares an URL to Visual Studio artifacts and the template variable {VS_2019_PREVIEW_VERSION} declares version of Visual Studio. Instead of hardcoded values for parameters VSBootstrapperURL and CustomImageName as it was [here](https://devdiv.visualstudio.com/XlabImageFactory/_git/ImageConfigurations?path=/Monthly/HelixBaseImages/VS2019Preview/Helix-Client-Enterprise-19H1-ES-ES-VS2019-Preview-Enterprise.yaml), we are going to use template variables. Here is an example of a fragment of a templated image definition:
```
{
    "Artifacts": [
        {
            "Name": "windows-vs-willowreleased",
            "Parameters": {
                "WorkLoads": "reduced",
                "Sku": "Enterprise",
                "VSBootstrapperURL": "{VS_2019_PREVIEW_URL}/vs_Enterprise.exe"
            }
        }
    ],
    "Destination": [
        {
            "StorageAccountName": "heliximgfctdncwus2",
            "CustomImageName": "Helix-Server-DataCenter-19H1-ES-ES-VS2019-Preview-Enterprise_{VS_2019_PREVIEW_VERSION}",
            "SubId": "84a65c9a-787d-45da-b10a-3a1cefce8060"
        }
    ]
}
```

_Note: Nested templated variables won't be supported._

Then we define variables file which will be stored in the repository:
```
{"VS_2022_URL":"https://aka.ms/vs/17/pre/675457237_947042406",
"VS_2022_VERSION":"17_0_3_1",
"VS_2019_PREVIEW_URL":"https://aka.ms/vs/16/pre/133508311_-1151188015",
"VS_2019_PREVIEW_VERSION":"16_11_2_1",
"VS_2019_URL":"https://aka.ms/vs/16/release/133508311_-1151188015",
"VS_2019_VERSION":"16_11_2_0"}
```

Given our example scenario, we will update variables VS_2019_PREVIEW_URL and VS_2019_PREVIEW_VERSION. And this change will be pushed into the repository. This change triggers our new build pipeline under Azure DevOps.

The pipeline executes command line tool which processes all payloads and substitutes all template variables from the variable file. For each payload it calculates hash and checks if this payload has already been sent. Images which are already in progress or completed won't be processed again. State will be persisted in a simple Azure storage table.

If needed it will be possible to force rebuild of images.

The pipeline won't run until all images are completed. It only post jobs, gets state of jobs and finishes. But it will be executed several times per day to check for completion of images. In a case any image build fails then the pipeline will fail and FR will be notified by email.

Once Helix custom images are generated, FR has to create an OSOB PR with updated image names. OSOB performs version test of Visual Studio as part of post validation  of PR/main build. This step can be taken as smoke test of new images.

Documentation of the Image Factory API can be found [here](https://devdiv.visualstudio.com/XlabImageFactory/_wiki/wikis/XlabImageFactory.wiki/6330/AccessingImageFactory).


## Take ownership of Helix custom image definitions

Making custom image definitions templated requires modifications. This is why we need to move all [definitions](https://devdiv.visualstudio.com/XlabImageFactory/_git/ImageConfigurations?path=%2FMonthly%2FHelixBaseImages) under our repository. I double checked that these definitions aren't shared and that there wasn't ever a need for changing them. Because of this, there isn't additional maintenance cost and it enables us to make changes fast. Also there is no risk that we need to transfer changes back to XlabImageFactory.

As part of the move I would suggest to start using JSON file format as it's expected input of the Image Factory. Having them in YAML would only mean that we will need to transform payloads before sending them. Only additional benefit of YAML are comments, but in our case these comments are copy pasted between all definitions and don't add any additional value.


## Proposition for extension of this epic

After images are successfully built, raise a new OSOB Pull Request updating image definitions with the latest images. This shouldn't take longer than one additional week of development and would save us a lot of time every week.


## Stakeholders

- .NET Engineering Services

## Risk

- What are the unknowns?

    Unknow is a way how we patch Windows images

- Are there any POCs (proof of concepts) required to be built for this work?

    POC for Visual Studio images was done before creating this one pager.

- What dependencies will this epic have? Are the dependencies currently in a state that the functionality in the epic can consume them now, or will they need to be updated?

    This epic depends on external service Image Factory. It's functionality is currently stable and no breaking changes are planned. I asked to notify our team in a case of changes.

- Will the new implementation of any existing functionality cause breaking changes for existing consumers?

    Implementation of this shouldn't cause breaking changes as all issues with images should be detected during OSOB validation phase.


## Serviceability

- How will the components that make up this epic be tested?

    Tested by unit tests.

- Identifying secrets (e.g. PATs, certificates, et cetera) that will be used (new ones to be created; existing ones to be used).

    Existing secrets:
	* image-factory-tenant-id
	* image-factory-client-id
	* image-factory-client-secret
	* image-factory-resource-id

    New secret:
    * image-factory-state-connection-string

- Does this change any existing SDL threat or data privacy models? (models can be found
in [core-eng/SDL](https://github.com/dotnet/core-eng/SDL) folder)

    It doesn't change SDL threat or data privacy models.


## Rollout and Deployment

- A new Azure DevOps build pipeline will take this tool from artifacts and executes it.

- In a case of issues, it is still possible generate images manually.


## Usage Telemetry
- This tool is internal only. Basic information about runs will be available from the pipeline history. We don't plan to include any additional data, unless they are requested. If we start experience problems with the image generation though, we might need to start gathering some reliability data.

## Monitoring
- Monitoring is done by result of the build pipeline. It will send email notification on any failure. If there is any issue, it should be picked up by FR.

## FR Hand off
- Will create documentation about
    - How to generate custom images with Visual Studio
    - How to use the tool in manual mode
    - What to do when pipeline fails the build


