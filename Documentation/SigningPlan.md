# Signing Plan
The goal of the signing plan is to provide guidance on how the tier 1 (T1) product repositories should sign binaries that are going to be published.

## Requirements
- Sign binaries via Microbuild.
- Avoid dependency on software installed on the machine, where possible
- Signtool will (batch) sign from a manifest (list of files).
  - This manifest can be checked in (explicit).
  - This manifest can be generated during the build (implicit).
- Validation in local, CI and official builds that all assets that need signing are going to be signed.

## Roadmap
1. (S137) Migrate SignTool from Repo tool set to Arcade.
2. (S138) Add SignTool to Arcade SDK.
3. (S138) Arcade should sign it's packages using the SignTool that is in the SDK.
4. (S138 - S139) Refactor the SignTool to accomodate scenarios for other repositories. Examples of the changes are:
    - Convert to MsBuild task.
    - Accept a manifest file from different source (implicit or explicit).
    - Read the strong name from the metadata of a build.

    **Note:** These changes depend on the decision taken according to the requirements of the Sign tool plan

5. (S140) Onboard one repository from T1 to use the SignTool from the SDK to sign its binaries.
6. (S140) Once validated in a repository, start onboarding the other T1 repositories.

## Sprint to dates
- S137: 6/11 - 6/29 
- S138: 7/2 - 7/20 
- S139: 7/23 - 8/10 
- S140: 8/13 - 8/31 
