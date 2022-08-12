# Improved Docker Container Image Lifecycle

As part of #10349 to improve our docker container security and sustainability, we need to improve the container image lifecycle. Currently, our container definitions are stable, but rarely updated, with some of the definitions dating back several years. We don't have means to ensure that all container images we use contain the latest OS patches and CVE fixes. One of the main points of this proposal is to ensure that the containers are updated regularly, accepting servicing updates form the OS on a regular basis. The major business goals of this work are to make sure that:

- Our container images are re-built regularly and they contain the latest underlying OS patches and CVE fixes
- There is a mechanism for updating the docker containers used by product teams so that they are always on the latest version of each container image
- There is a process and tools implemented for identifying and removing images that are out of date
- There is a process and tools to delete old container images (older than 3-6 months) from MCR
- All images used in the building and testing of .NET use Microsoft-approved base images, either Mariner where appropriate, or [Microsoft Artifact Registry-approved images](https://eng.ms/docs/more/containers-secure-supply-chain/approved-images) where Mariner is insufficient

## Stakeholders

- .NET Core Engineering
- .NET Acquisition and Deployment
- .NET Product teams

## Risks

- Will the new implementation of any existing functionality cause breaking changes for existing consumers?

The major risk in this portion of the epic is finding and updating all container usages by product teams, and making sure that moving them to the latest versions of the container images doesn't break their builds/tests because of missing artifacts. Our goal is to use docker tags to label the latest known good of each container image, and replace usages of specific docker image tags with a `<os>-<version>-<other-identifying-info>-latest` tag. That way, much like with helix images, their builds and tests will be updated automatically when we deploy a new latest version. In the transition to latest images, we may find that older versions of a container may have different versions of artifacts installed on those containers, which could affect builds and tests. We will need to be prepared to help product teams identify these issues and work through them.

- What are your assumptions?

Assumptions include:
- The Matrix of Truth work will enable us to identify all pipelines and branches that are using docker containers and which images they are using
- We will be able to extend the existing publishing infrastructure to also idetify images that are due for removal
- All of our existing base images can be replaced with MAR-approved images
- Most of the official build that is currently built in docker containers can be built on Mariner
- MAR-approved images are updated with OS patches and CVE fixes

- What are your unknowns?

Unknowns include:
- How will we identify the LKG for each docker image?
- What testing is currently in place for docker images, so that we can have confidence that updating the `latest` image will not break product teams?
- What is the rollback story for the `latest` tagging scheme?
- If the MAR-approved images are not updated on a regular basis, how do we apply OS patches and CVE fixes to the base operating systems?

- What dependencies will this epic/feature(s) have?

This feature will depend heavily on MAR-approved images (and whatever updating scheme they have for updating base images), as well as the existing functionality for building and publishing our docker container images. We will want to expand the existing functionality to allow us to 1) identify the last known good of each docker image and 2) tag that LKG with a descriptive `latest` tag.

## Serviceability of Feature

### Rollout and Deployment

As part of this work, we will need to implement a rollout story for the new tagging feature. We do not want every published image to immediately be tagged as `latest`. In fact, we may want to implement two different tags: `latest` and `staging`. In this scheme, we would branch the `dotnet-buildtools-prereqs-docker` repo so that we have a production branch. Every image published from main would be tagged `staging` which could then be used in testing, much like the images in our Helix -Int pool. This tag would be used for identifying issues ahead of time so that when we rollout, we will be more confident that the images we are tagging as `latest` will be safe for our customers. The rollout would be performed on a weekly basis, much like our helix-service, helix-machines, and arcade-services rollouts. We roll all of the known good changes in the main branch to production, and publish those images with the `latest` tag. This would allow us to reuse the same logic for both staging and latest.

We will also need a rollback story so that if an image breaks a product team's build or test, we can untag that image and retag the previous `latest` image. A rollback should be as simple as reverting a previous change and publishing the images at the new commit. While this image may be identical to a previously published image, it will effectively be treated as a new version.

### FR and Operations Handoff

We will create documentation for managing the tags so that when a rollback needs to occur, FR will be able to make those changes. Additionally, we will create documentation and processes that can be used by Operations and/or the vendors to handle any manual OS/base image updating or removing of old and out-of-date images from MCR, as necessary. We will also create documentation for responding to customer requests for new docker images, including where to get the base images, and how to install required dependencies (though that is coming in a different one pager).