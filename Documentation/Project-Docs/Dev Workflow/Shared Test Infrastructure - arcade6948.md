# Developer Workflow - Shared Test Infrastructure

As the complexity of the test runs for .NET repos has increased, there has proved to be a need for added tooling and features
surrounding the execution and management of tests, as well as improvements in both the time and the cost of running tests.

The way that tests are managed and executed accross the .NET has become increasingly fractured, making it difficult to provide
these needed features in a centralized way. In reaction to this, the various product teams have had to tackle some of their
difficulties with the existing systems independently by building custom tooling around the Helix test infrastructure managed by
Engineering Systems.

The shared test infrastructure effort aims to provide a more consistent experience that the product teams can take advantage of for the
execution and management of test runs as described in the epic [Improve Dev WF by providing shared test
infrastructure](https://github.com/dotnet/arcade/issues/6948)

### Stakeholders

- Product teams' developers
- Product infrastructure teams
- Product teams' management
- Engineering services developers
- Engineering services management

### Risk

- We need to make sure to work closely with the product teams to ensure the infrastructure suits their needs.
- Inability to account for the various edge-case scenarios in the various product repositories.
- The various product repositories have varied workflows and requirements that might prove incompatible with each other when we're
  coming up with solutions.
- Changes to the helix Python scripts are risky.
- We don't yet know if we will have to request new features against Azure DevOps or GitHub, or any limitations in their systems
  that we will have to work around.
- Onboarding to the new infrastructure proves too difficult for repositories, making feature adoption slow.
- Moving towards agentless waiting of Helix jobs doesn't provide the build time or cost savings expected. 

### Additional one-pagers

The following documents deal with specific aspects or features of the epic

- [Agentless Helix](./agentless-helix.md)
- [Measuring Success](./Success-Measures-arcade6948.md)
- Shared Test Infrastructure (./shared-test-infra-arcade6948.md )
- Dump 2.0 (TBD) 

### Service-ability of Feature

As with previous Dev WF efforts, we will create documentation for any new features, both for the product team users and for hand
off to the First Responders v-team once the epic is complete.
