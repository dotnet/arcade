# One-Pager for Epics

## How to Use

The goal of a one-pager is to document expectations and goals for an epic or feature before embarking down the path of implementation. The one-pager is also meant for the v-team to think about how we are going to measure the impact of the epic to our customers and how the changes will be supported by the First Responder v-team once the epic is complete. It's not meant to document specific implementation details, but as a means to think about the epic or feature as a whole and provide a place to discuss what the stakeholders want, what challenges the v-team could face and how to mitigate those challenges, what parts need more clarity and research, et cetera. 

Below are questions to think about when filling out the parts of the template. The end result isn't to have a dissertation, but a short "one-page" document of what the epic or feature will do, how we'll measure impact/usage telemetry, and how the engineering services team will support it. **Feel free to use the template in whole or in part to create your one-pager**. 

In order to align with Epic Content Guidance, one-pagers should be stored in a folder called `One-Pagers` within the `Documentation` folder on `core-eng`. The name the one-pager is saved as should match the name of the epic it is associated with and included the epic issue number (for easy reference). Example: *Coordinate migration from "master" to "main" in all dotnet org repos - core-eng10412.md*. Use the PR to document the discussion around the content of the one-pager. 

After all discussions have been resolved, the resulting one-pager document should be signed-off (this does not need to be a formal process) by stakeholders (e.g. v-team members, epic owners, et cetera) and then linked to the associated epic's GitHub issue for discoverability.

Some of the content for this template was taken from [this article](https://medium.com/@johnpcutler/great-one-pagers-592ebbaf80ec), which goes into more depth about one-pagers.

## One-Pager Template

### Stakeholders

- Who are the stakeholders (e.g. folks who will be using this feature; folks who are requesting this work; folks who need to "sign-off" on the epic)

### Risk

- What are the unknowns? 
- Are there any POCs (proof of concepts) required to be built for this epic? 
- What dependencies will this epic have? Are the dependencies currently in a state that the functionality in the epic can consume them now, or will they need to be updated? 
- Will the new implementation of any existing functionality cause breaking changes for existing consumers? 
- Is there a goal to have this work completed by, and what is the risk of not hitting that date? (e.g. missed OKRs, increased pain-points for consumers, functionality is required for the next product release, et cetera)

### Serviceability

- How will the components that make up this epic be tested? 
- How will we have confidence in the deployments/shipping of the components of this epic? 
- Identifying secrets (e.g. PATs, certificates, et cetera) that will be used (new ones to be created; existing ones to be used).
    - Instructions for rotating secret (if the secret is new)
- Does this change any existing SDL threat or data privacy models? (models can be found in [core-eng/SDL](https://github.com/dotnet/core-eng/SDL) folder)
- Does this require a new SDL threat or data privacy models?
- Steps for setting up repro/test/dev environments?

#### Rollout and Deployment
- How will we roll this out safely into production?
    - Are we deprecating something else?
- How often and with what means we will deploy this?
- What needs to be deployed and where?
- What are the risks when doing it?
- What are the dependencies when rolling out?

### Usage Telemetry
- How are we tracking the “usefulness” to our customers of the goals? 
- How are we tracking the usage of the changes of the goals? 

### Monitoring 
- Is there existing monitoring that will be used by this epic? 
- If new monitoring is needed, it should be defined and alerting thresholds should be set up. 

### FR Hand off
- What documentation/information needs to be provided to FR so the team as a whole is successful in maintaining these changes? 
- If you have created new monitoring rules - what tools/processes should FR use to troubleshoot alerts
- If existing monitoring is used, do the parameters need to be updated to accommodiate these new updates
