# Product Construction alignment One Pager

With the new Product Construction (ProdCon) and Engineering Services (ES) charters, there is a need to align resources, like the code and Azure subscriptions, with the new charters.
This document will focus on the Product Construction, and the resources that will be owned by that team. This is mainly the Maestro service, and other tools that come with it, like Darc and the VMR tooling. 

## The goals

The goal of the alignment is to have the Product Construction team own all assets, processes and other responsibilities that are align with their charter. These include, but are not limited to:

- The code
- The pipelines
- The services
- Azure resources
- Rollouts
- Issue triage and management
- S360
- SDL
- Budget maintenance

The alignment must not affect the quality of services and support that we provide to our customers

## The reasoning
- With the new charter defined, the logical next step is to separate existing resources into a separate space, so the team responsible for it can own everything
- The arcade-services repository, where most ProdCon code is currently has a number of tooling and services. Recently we've had many cases where we couldn't roll out the whole repo, because of issues with an internal project. Considering Maestro is a critical component in the .NET world, decoupling it's rollouts from the rest of the projects in its repo is a good idea

## Stakeholders
- The DotNet Product Construction team
- The DotNet Engineering Services team

## Risks
- Charter alignment will involve Maestro Azure resources migration, this means we will have some down time, during which we won't be able to flow dependencies between repositories, promote builds to their respective channels, etc.. We will have to figure out a way to cause minimum disruption for our customers. This means picking a good time to do the migration, when we're not working on servicing or a new Preview, making a detailed plan on what needs to happen, and having the ability to revert everything, in case it goes wrong.
- The Maestro BuildAssetRegistry contains very important information for .NET releases, it is of utmost importance that we handle the migration of it with care, and not lose any data inside of it.

## Rollouts
- The ProdCon team will be responsible for rollouts of it's own services
- The rollout schedule won't change, so we don't confuse or customers

## S360/SDL
- The ProdCon team will be responsible for handling S360 issues for its own Service Tree entry
- The ProdCon team will be responsible for doing regular SDL Compliance Assessments for its own services

## High level steps
Currently, all of the resources are shared between the teams:

- The code is in the arcade-services repo
- The Azure resources are all under the Helix subscription

To achieve the alignment, we will:

- Create the new Subscriptions under the ProdCon Service Tree
- Move the ProdCon Azure resources into the new ProdCon subscriptions
- Identify all non ProdCon code in the arcade-services repo
- Move all non ProdCon code into a different repo

## Unknowns
- There is a lot of code in the arcade-services repo that's shared between between various ProdCon and Engineering Services. In the future, this code will have to be considered as shared. We will need to create a maintenance process for these for things like ComponentGovernance, so both teams are happy.
- There are tools like the Secret Manager would want to use. We need to define a process for scenarios like wanting support for a new type of secret, which team does the implementation.
- The BAR database is used in many different places (for build promotion, by the staging pipeline, etc..). We need to identify these places and figure out a replacement strategy for the BAR database in the new ProdCon subscription.