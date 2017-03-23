# Overview
At a high level, the following big steps represent the work to get a dev a reproduction environment
1. The dev chooses a target environment
1. The right machine (VM or otherwise) is aquired and access info made available
1. Machine is setup and configured so a reproduction can happen
1. Product is (re)built and test run  (yes for now, we'll rebuild the whole thing)
1. Dev investigates failure

# Dev chooses target environment

### Requirements
- Be able to choose a specific hash, or latest
- Button on MC?  

### Implementation Notes
For an initial "strawman", the current idea is to use Jenkins to basically redo a run, but not publish or update the PR, and then give the machine to the dev to poke around with.


# Aquiring machines
There are two main methods to aquire machines for a reproduction environment: 1) Dev Test Lab (DTL) for VMs and 2) Asset Explorer (AE) for physical hardware like Macs, X64, etc.

### Requirements
- Common interface to "check out" a machine (VM or otherwise)
- Checking out a machine comes with sufficient data to connect and configure
- The machine should be delivered in a known state which can reasonably be setup to repro

### Implementation Notes
For an initial "strawman", the current idea is to use Jenkins to basically redo a run, but not publish or update the PR, and then give the machine to the dev to poke around with.

#### Tools/Systems that will probably help in the future
*DTL*
- VM image management happens here (would love to find a way to share with CI and VSTS)
- Special artifacts selectable
- https://microsoft.sharepoint.com/teams/DD_DDIT/DDITLabs/Pages/Azure-DTL.aspx

*Asset Explorer (AE)*

AE is a tool (originally from Office) which manages inventory in "pools", allowing for check out and check in.  It does not manage the machines directly, but is simply a database of sort to keep track what's available and who has what.  The good news is that it has a web API.

- Our pool is \STB\DevDiv\DotNet
- Web services url is http://aee/ws
- To install the AE client is http://aee/installae.  IE is needed to install the client.
- Contact: Dale Hirt in DDIT

# Machine environment setup / configuration
Once a machine is aquired, additional setup is still required in order to support building the product and tests (again) so a reproduction can occur.

### Requirements
- Prereqs are either installed, or verified to be installed
- Debugger and compilers installed
- Machine config 
- Exact hash is restored (GitHub is what's initially supported)
- Matching symbols (especially for when we find a way to NOT rebuild the product)
- Logs from original failure available
- Shares already setup
- Make it easy to re-run the failing test  (initially, this might just be run all the test again...hopefully we can do better)

### Implementation Notes
For an initial "strawman", the current idea is to use Jenkins to basically redo a run, but not publish or update the PR, and then give the machine to the dev to poke around with.

### Misc reference material
- dotnet-ci jumping off point: https://github.com/dotnet/dotnet-ci
- buildtools groovy example: https://github.com/dotnet/buildtools/blob/master/netci.groovy 


# Product and Tests are Built
The right first step is to just rebuild the product again.  Not ideal, but crawl, walk, run....

### Requirements (not all are needed initially)
- Be able to easily rebuild a single test so that it can instrumented  (script file already there?)
- Be able to easily replace bits on the repro box from the dev box
