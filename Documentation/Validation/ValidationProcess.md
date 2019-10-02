# Validation Process Overview

Business Requirement: The process for running tests and reporting results uses a standard model across all services

Currently, we have tests projects sporadically throughout our projects, no consistency in how the tests are set up, no consistency in where to find results, or guidance on how to add more tests and ensure that they run at the appropriate times. One of the goals of this epic is solve these issues. 

## Consistency in Testing

This epic will define a standard model that we will follow in order to:
- Create new unit, functional/integration, and scenario tests
- Extend the tests that we have
- Hook them into appropriate pipelines or stages so that they can run during PRs/deployments/scheduled jobs

Ideally, the test projects should exist in the same solution with the deployable projects/services. The test projects will be able to be accessed by Azure DevOps pipelines/stages to run during certain events (e.g. PRs, deployments, scheduled jobs). For scenario tests, we can employ the use of categorization so that we can run subsets of the scenario tests in our different pipelines/stages. 

### Scenario Tests

Scenario tests should encompass things like common functionality used by our customers, functionality that was able to reproduce a problem in our service (e.g. load test that was able to reproduce the issue in AppInsights), et cetera. The tests should be categorized so that they can be run during specific pipelines: 

#### Pre-deployment
Examples of pre-deployment scenario tests should include: 
- Can we connect to the Service Fabric cluster that we need to deploy to?
- Do the secrets we use exist in the appropriate Key Vaults?

#### Post-deployment
Examples of post-deployment scenario tests should include: 
- Testing endpoints in Helix API
- Deployed service health check (e.g. are they running, are they returning expected results, et cetera)
- Was the Helix database schema changed appropriately? 

#### Nightly
Examples of nightly scenario test should include: 
- Service Fabric Chaos test
- Load testing sending jobs to Helix queues

## Code Promotion

The gates for code promotion are as follows: 
- **Pull Request for feature branch**: When development code is complete, it should pass checks in the pull request pipeline. If not, it cannot be merged into the master branch. 
- **Post-deployment checks and scenario tests for staging**: When a build is deployed to staging, there will be a suite of tests ran against it. If it does not pass these tests, the code cannot be merged into the production branch. 
- **Post-deployment checks and scenario tests for production**: When a build is deployed to production, there will be a suite of tests ran against it. If it does not pass these tests, a rollback to the previous deployment should occur. 

## Consistency in Reporting

When our tests fail, we want to be able to see what failure(s) occurred so that we can remediate the issue(s). We want to have a standard way for the tests to report on the test results, and allow us to see the health of our builds and deployments based on the tests that have ran. 

## Guidance

Documentation on how to do the above is an absolute must. We want to ensure that other developers can expand upon what we've started, so we will provide guidance with how we expect the test project structure to look like and how to hook them into the pipelines/stages. 

## Validation and Deployment Workflow

Ideal developer workflow from dev environment to production: 
- Developer is responsible for writing code for feature, unit/functional tests to cover code written for feature, and expanding on any scenario tests that changes the way the service works. 
- When the feature is completed, the developer should open a pull request to run validate their code and upon passing validation, merge into the master branch. 
- Pre-deployment checks on staging should occur to see if the services in staging are healthy for us to deploy to. If not, we should investigate and resolve the problems before we can deploy. 
- Code is build and deployed to staging.
- Post-deployment checks on staging should occur to ensure that the code we deployed is working as intended. This code is eligible to be merged into the production branch. 
- Similarly, we'll have pre-deployment checks on production prior to deploying our code to production. 
- Code is deployed to production. 
- Similar post-deployment checks on production. 
- Also, a nightly job will run that will handle more intense tests that will require more load than we want to be running during the day. 

![Validation and Deployment Workflow](Images/ValidationDeploymentWorkflow.svg)
