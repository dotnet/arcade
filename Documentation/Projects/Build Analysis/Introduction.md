# Build Analysis

The Build Analysis Check is a service to improve the Pull Request experience by highlighting build and test information most helpful to .NET developers. Its goal is to make Pull Request results more actionable.

## Contacts

Direct access to the V-Team is available (and welcome!) via the Teams channel "V-Team: Dev WF (PRs)" in the [.NET Core Eng Services Partners Team][]. You may also contact the team via any of the usual means to [Engineering Services][].

## How does it work?

- Highlight most important failure information
- Add context by combining information in Azure DevOps and Helix
- Reduce distance to most helpful analysis tools, such as Azure DevOps Test Result History for a particular test or the Helix artifact logs

## What _specifically_ does it do?

See the [specifics](Specifics.md) document.

## How do I get it?

Build Analysis is enabled on a per-repository basis. Contact the "Dev WF" to request it be enabled in your repository.

## How do I use it?

Once enabled, a new GitHub check suite will be included in all pull requests. Navigate to the "Checks" tab, then look for the ".NET Helix" suite.


[.NET Core Eng Services Partners Team]: https://teams.microsoft.com/l/team/19%3aa88bb61ffc1a4392ad38ebbc526c86f8%40thread.skype/conversations?groupId=4d73664c-9f2f-450d-82a5-c2f02756606d&tenantId=72f988bf-86f1-41af-91ab-2d7cd011db47 ".NET Core Eng Services Partners"

[Engineering Services]: https://github.com/dotnet/core-eng/wiki/How-to-get-a-hold-of-Engineering-Servicing "How to get a hold of Engineering Servicing"