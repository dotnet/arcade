# Improved Container Inventory Governance

As part of [#10123](https://github.com/dotnet/arcade/issues/10123) we want to improve the way we handle the software installed on our docker containers. Currently we install software from a few different locations: from the repo package managers (most things), and from the open internet (Node.js for example), where we don't run any checksums validation. We also currently don't have means to ensure that all the software we have installed is on supported versions. We also want to start using stable docker tags, so our customers get the OS and software patches, without having to change anything. This brings in an additional layer of complexity, because there is a small group of software (clang, cmake, ...) that, with a major update, might affect the build process, will be known as "breaking software" in the rest of the page. The major business goals of this work are to make sure that:

 - Where possible, install all software from the repo package managers
 - Implement checksum recording and validation for components that are installed from the open internet
 - Ensure that we're not automatically updating major versions of software that might affect the build process

## Stakeholders

- .NET Core Engineering
- .NET Acquisition and Deployment
- .NET Product teams

## Risks

- Will the new implementation of any existing functionality cause breaking changes for existing consumers?

The major risk in this portion of the epic is identifying the full list of software needed on the docker images, especially the subgroup that can affect the build process, if updated without knowledge. We need to make sure that the new container images don't break our customers workflows, because of missing tooling. Our goal is to have a way to update tooling on our docker containers, without our users having to change the dockerTags they use for their builds (in case of non breaking software updates), and to have to use PRs otherwise. It is also important that we keep everything open source.

- What are your assumptions?
    - We will be able to identify the full list of needed software with the help of the product teams
    - Most of the software we need will be available through the distro package managers
    - We'll be able to safely download the remaining software from somewhere else (with checksum validation)
    - We will probably need to lock down access for updating our containers to dnceng, which means we will need a process through which people can ask for new software/updates

- What are your unknowns?
    - Will we have to provide specialized containers for each workload, or will we able to use the same image for multiple workloads?
    - Can we have multiple versions of the tools on the same image (multiple versions of clang, for example)
    - What testing is currently in place for docker images, so that we can have confidence that updating the image will not break product teams?
    - How will we identify when the software that we don't install through the package managers needs an update?

- What Dependencies will this epic/feature(s) have?

This feature will need to align with the new tagging schema. We might have to include the "breaking software" versions in the naming schema somehow. That way, our customers would get the latest versions of the non "breaking software" tools, knowing that their builds won't be affected by them.

## Serviceability of Feature

### Rollout and Deployment

Much of the rollout story will be the same as for the Improved Docker Container Image Lifecycle feature. We'd want to have a staging environment before we'd start using new and updated images on the production workloads. Rollouts would be performed on a weekly baisis where we'd roll out all known good changes and publish a new set of production images. We also need to be able to roll back onto previous image versions, in case of issues.

### FR and Operations Handoff

Before handing off this feature to FR, we will document all newly created processes that include (but are not limited to): updating a tool to a new version, adding new tools to a docker image, etc...
