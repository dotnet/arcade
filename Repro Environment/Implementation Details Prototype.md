#  Prototype Implementation Details
The following document shows the implementation details for the prototype of the repro environment solution according to the requirements.

# Requirements
- The dev experience should be "simple" (single click) and "fast" (Less than 15 minutes to create the environment)
- Ability to config repro capability (saving data for later machine creation on failture) to repro the failure
- Ability to request a job to repro (automatically creates the environment)
- Must work for Windows and Linux
- Needs to work for Jenkins
- Solution is for devs who work at Microsoft only
- Checking out a machine comes with sufficient data and access to connect and configure 
- The machine should be delivered in a known state which can reasonably be setup to repro
- Prereqs are either installed, or verified to be installed
- Exact hash is restored (GitHub is what's initially supported)
- Provide method to move files onto/off-of machine
- Support retention policy for the snapshot/workspace data
- Decommision once dev indicates they are complete with repro (rudamentary)

# Nice to Have
- A workable version of debugger and compilers installed
- Matching symbols - especially for when we find a way to NOT rebuild the product
- Be able to easily build the product, build the test, and then run the tests
- Be able to easily run the failing tests and see the results
- Be able to attach a debugger to the test
- Be able to reasonably produce a "report" of all outstanding environments and their age and dev
- Ability to download workspace for local (or custom) repro

## Progress
For information about the progress, please go to the following Epic:
- Dev Test Failure Repro [408](https://github.com/dotnet/core-eng/issues/408)

## Considerations:
Please refer to the section [Developers involved in the POC](https://github.com/dotnet/core-eng/blob/master/Documentation/Project-Docs/Repro%20Environment/Implementation%20Details%20POC.md#developers-involved-in-the-poc) of Implementation details POC.
