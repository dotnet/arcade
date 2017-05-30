# Overview
At a high level, the following big steps represent the work to get a dev a reproduction environment
1. The dev chooses a target environment
1. The right machine (VM or otherwise) is aquired and access info made available
1. Machine is setup and configured so a reproduction can happen
1. Product is (re)built and test run
1. Dev investigates failure
1. Environment is decommisioned once the dev is done, or according to policy

The immediate goal is to find the quickest implimentation that will work for every participant in the .NET Core eco-system.  From there, we can find ways to improve sustainability as well as flesh out feature sets.  For now however, biggest "bang for the buck" is the goal.  From there, we will have learned much and can chart an even better path.

For the start of a more complete requirements list, please read the rest of this mini-doc.

**Requirements needed for the first round are noted with a (*)**

# Dev chooses target environment

### Requirements
- *Dev must have Microsoft credentials that we can manage
- Be able to choose a specific hash, or latest from a given repo
- Button where the dev saw the failure (for example, Mission Control) OR single command line to request 
- VSTS git hashes are currently a non-requirement

# Aquiring machines

### Requirements
- *Common interface to "check out" a machine (VM or otherwise)
- *Checking out a machine comes with sufficient data and access to connect and configure 
- *The machine should be delivered in a known state which can reasonably be setup to repro
- Devs needs the ability to list all environments they've asked for, including current state (e.gl. if they're ready or not)
- Dev should have the ability to give other devs access

# Machine environment setup / configuration
Once a machine is aquired, additional setup is still required in order to support building the product and tests (again) so a reproduction can occur.

### Requirements
- *Prereqs are either installed, or verified to be installed
- *Debugger and compilers installed
- *Exact hash is restored (GitHub is what's initially supported)
- *Matching symbols (especially for when we find a way to NOT rebuild the product)
- Logs from original failure available
- *Provide method to move files onto/off-of machine
- Make it easy to re-run the failing test  (initially, this might just be run all the test again...hopefully we can do better)

### Misc reference material
- dotnet-ci jumping off point: https://github.com/dotnet/dotnet-ci
- buildtools groovy example: https://github.com/dotnet/buildtools/blob/master/netci.groovy 

# Product and Tests are Built

### Requirements (not all are needed initially)
- *Be able to easily build the product, build the test, and then run the tests
- Be able to easily rebuild a single test so that it can instrumented  (script file already there?)
- Be able to easily replace bits on the repro box from the dev box
- Portable Linux support is needed (build on one platform, but run on another)

# Test is Run
This is where the "repro" actually happens

### Requirements
- *Be able to easily run the failing tests and see the results
- *Be able to attach a debugger to the test
- Be able to run only the failing test
- Be able to find "edge case" (but very expensive) test failures quickly and reasonably

# Repro Environment is Decommisioned
Once the dev is done, the environment needs to be decommisioned.

### Requirements (not all are needed initially)
- *Decommision once dev indicates they are complete with repro
- *Be able to reasonably produce a "report" of all outstanding environments and their age and dev
- Decommision environments based on policy (e.g. time, last used, etc)

# Known Challenges
- Additional cost of VM's might be high.  Need to look for ways to optimize.
- Access to the repro VM's might be difficult.  Namelyl with permissions and potentially IPs.
- If Jenkins is chosen as an initial implimentation path, there may be scaling and maintenance challenges.
- We still want to share VM/Machine aquisition, and if we juse Jenkins initially, this goal would not be significantly furthered.

# Tools/Systems that will probably help in the future
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