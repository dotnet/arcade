# Shared Docker Images One Pager

## Goals and Motivation

Docker images are increasingly used in the builds and testing of .NET. The images used by the product teams are, however, frequently out of date and in need of patching. This epic seeks to rectify this issue by:

- Consolidating docker image creation and helix image creation in OSOB using the same artifacts
- Updating docker image publishing so that we can publish a "latest" tag for product repos to settle on
- Create a strategy for EOLing old images
- Solidify policy for servicing docker images

## Stakeholders

- .NET product teams
- DNCEng team
- A&D team

## Proof of Concept

A quick POC was developed to show that we can convert the [helix ubuntu-2204 Dockerfile](https://github.com/dotnet/dotnet-buildtools-prereqs-docker/blob/main/src/ubuntu/22.04/helix/amd64/Dockerfile) to a helix definition, which then can generate the dockerfile that can be built and run. This POC showed us that there are several changes that need to be made in various places, including:

- `osob-cli`
  - Not require additional dockerfiles to be checked in to supply setup information. 
  - Any run commands should be converted into artifacts
  - The `FROM` command should be added as part of the builder
  - Allow for not using the helix clientSettings.zip, which should be unnecessary for docker images that aren't meant to replicate helix queues
- Helix artifacts
  - Helix setup artifacts should be skipable for docker images, which do not require the helix client, etc.
  - Helix artifacts that assume helix setup should be adjusted for docker images not requiring helix setup
  - Additional artifacts will need to be created to match some of the functionality supplied in the current dockerfiles
- Adding docker specific functionality
  - Definitions will need to be able to supply a docker base image that will be used

The POC can be found in this [draft PR](https://dev.azure.com/dnceng/internal/_git/dotnet-helix-machines/pullrequest/24290).

## Risks

* Will the new implementation of any existing functionality cause breaking changes for existing consumers?

It should not. Old docker images will still be available, though we will be encouraging teams to move to the new images that are servicable. Additionally, the newly created docker images will be designed to match the existing ones, with all the same artifacts installed. We will do extensive testing to compare existing docker images to the new ones before rolling out the changes.

* What are your assumptions?

Our assumptions are that we will mostly be able to reuse infrastructure we already have, for example, the `create-docker-image` command in the `osob-cli` and the infrastructure for publishing docker images to the container registries. The `yaml` for the latter seems rather complicated, though, so we will need to work with the A&D team to figure out the right way to move the publishing code to dotnet-helix-machines. We will also want to extend `create-docker-image` to be able to automatically create dockerfiles for every one of the definitions we will create, rather than have it be a one at a time, rather manual process. Today, because the tool was designed to help the team test their helix definitions before putting up a PR without having to create a VM, you have to supply the name of the queue that you want to convert into a dockerfile. We will want to have the command take a set of `yaml` files and create docker images for every definition found in those `yaml` files.

Additionally, we are assuming that the artifacts that we have can mimic the hand-written docker code that currently exists. A major part of this epic is simply going to be migrating dockerfiles to yaml definitions using our artifacts. While this is relatively straightforward, there are some instances where this may be difficult. For example, in the POC, we found that docker images do not have sudo installed on them by default, and our package manager scripts assume that sudo is on the path so that we could create the `/etc/helix-prep` directory, which is used by `linux-add-package`. There is a bit of a race condition in this scenario, suggesting that for docker images, we may want to wholesale skip the creation of the `helix-prep` directory (and all other helix setup steps, which are unnecessary for docker images), which means the package manager scripts will need to be modified in the docker case.

* What are your unknowns?

Part of this work would essentially closed source the docker image definitions in https://github.com/dotnet/dotnet-buildtools-prereqs-docker. Is that acceptable? Should we try to open source the image, queue and artifact definitions in dotnet-helix-machines? Will we face backlash if the definitions in the current repo are taken back into closed source?

* What dependencies will this epic/feature(s) have?

Our best current strategy is to reuse the logic in `create-docker-image` in the `osob-cli` to convert the yaml format we use for helix definitions into docker images. We would need new definitions for docker images, since there are several pieces of data that are not applicable to docker images, and our docker images may require different artifacts, etc. However, this will allow us to reuse several pieces of technology we have already written and know: the helix definition format, helix artifacts, and a tool that can already convert the definitions into docker image definitions.

We may also need to reuse and depend on the docker image publishing work in https://github.com/dotnet/dotnet-buildtools-prereqs-docker.

## Serviceability

By merging docker image creation into OSOB's infrastructure, we can also rely on the servicing and testing features of OSOB, as well as the deployment pipeline and rollout process.

### Rollout and Deployment

* How will we roll this out safely into production? Are we deprecating something else?

We will rollout the new docker images as part of the dotnet-helix-machines rollout process. The major change here will be pushing the images created to the correct container registries. This is currently done by the dotnet-buildtools-prereqs-docker pipeline, which we will be deprecating in favor of the new process.

* How often and with what means will we deploy this?

We will deploy the docker images as part of the dotnet-helix-machines deployment, which is done on a weekly cadence.

## FR Handoff

We will provide documentation about the docker image creation tool as well as the process for publishing the images to docker, both of which will need to be maintained by FR.