# Validation for Helix API and Services

## Infrastructure Design
Move Helix API and Services Build and Deployment to AzDO Stages. The following are the pipelines:
- **PRs** : <br />
    Build -> Validate stages. Validate stage will include tests like the following:
    - API Validation like comparing API signatures to what is currently deployed using swagger
    - Unit and Functional Tests

- **CI / Staging**: <br />
    Build -> Validate -> Deploy -> Post-Deploy Validation. 
    - Validate here would cover pre-deployment checks
    - Post-Deploy Validation would include E2E scenarios on the services deployed

- **Production**:<br />
    Build -> Validate -> Deploy -> Post-Deploy Validation.
    - Same as Staging pipeline but against Prod branches
    - Validate should compare DB , Secrets mismatches between Staging and Prod.

- **Nightly**:
    - Bin Skim
    - Long running job like CoreFx with its 6B tests run Helix SDK from Arcade -> Helix API Int -> Helix DB Int -> AzDO Test Explorer.

## Top Scenarios

-  E2E Test that builds a fully arcade-ified repo using:
	- Build on agents from Helix Staging environment using the Int pool provider
    - Sends real test work items to int Helix API to run on Int helix machines (*would need to have higher than 1 scale or long timeouts) 
	- Runs said work items on broad variety of non-on-prem hardware
	- Reports results to Azure DevOps
- Helix API
	- Test all APIs current version
	- Test all APIs last two versions
- E2E tests using OnPrem queues for Helix Int
- Test for usage of Secondary Queues (i.e. send a secondary work item to a different queue, make sure both work items process as usual)
    - Test Docker scenarios (need to teach the test system “these queues have docker and thus can use the docker daemon to run some tests)
	- Using the Helix SDK to create and send jobs. 
	    - Verify that failed tests are reported correctly
		- Verify that passed tests are reported correctly
		- Verify that the data makes it into our database accordingly
		- Verify that expected workitems are deadlettered if sent to a queue that is deadlettered
	- For 2.0 and 2.1 servicing: Send a job using BuildTools Helix Client

## High Priority Validation
- Health checks for all Service Fabric services. 
	- Are the services running? 
	- Health state of the services running
	- Check for unhandled exceptions
- Schema validation and code-ify the HelixData/HelixDataProd SQL database. 
- Test for successful heartbeating of both pr-* and staging machines on validation.
	- Bonus: Test machines heartbeat after reboot

## Lowest/No Priority Scenarios
- Anything for Mission Control
- Anything for Repro Tool

## Where do the Tests live?
- Unit and Functional Tests for a service will live in the same solution of the service.
- E2E Test Scenarios will live in a solution of its own in [same repo](https://dev.azure.com/mseng/Tools/_git/CoreFX%20Engineering%20Infrastructure)

## Features/Services in Scope
- Helix API
- Data Migration Service / Health Monitoring SQL (HMS)
- Helix Services hosted on Service Fabric
    Services that run on Service Fabric
    - DeadLetterProcessor
    - HelixController - configures and manages the Helix queues
    - JobFinishedProcessor
    - MetricsAlerter
    - MetricsObserver
    - SQLCleaner
    - WebJobService - Project that sets up the jobs in Service Fabric. 

    Tests for SF Services should test 
    - availability and health of cluster and service
    - validates the service is doing what its supposed to do
    - test whether a deployment will succeed



