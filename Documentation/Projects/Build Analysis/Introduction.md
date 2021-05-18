# Build Analysis

The Build Analysis Check is a service to improve the Pull Request experience by highlighting build and test information most helpful to .NET developers. Its goal is to make Pull Request results more actionable.

## What does it do?

High-level features include:

- Highlight most important failure information
- Add context by combining information in Azure DevOps and Helix
- Reduce distance to most helpful analysis tools, such as Azure DevOps Test Result History for a particular test or the Helix artifact logs

For more details, see the [specifics](Specifics.md) document.

## How do I get it?

Build Analysis is enabled on a per-repository basis. Contact dnceng to request it be enabled in your repository.

## How do I use it?

Once enabled, a new GitHub check suite will be included in all pull requests. Navigate to the "Checks" tab, then look for the ".NET Helix" suite.

## Frequently Asked Questions

### Is this useful even if my project does not use Helix?

Yes! The Build Analysis result leverages Azure DevOps history and information to provide context and workflow improvements. Though it may bring additional information for Helix-based execution, it is not required. 
