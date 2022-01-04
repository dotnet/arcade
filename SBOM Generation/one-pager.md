## Generating the Software Bill of Material (SBOM)

## SBOM
The Executive Order(EO) and The National Telecommunications & Information Administration (NTIA) report defines an SBOM as a formal record containing the details and supply chain relationships of various components used in building software. On May 12, 2021, the U.S. Presidential EO, section 4(e)(vii) is requiring all software sold to the federal government to provide a Software Bill of Material (SBOM). 

SBOM is usually a single file (such as .json) that captures this information about the software from the build. Microsoft has decided to use Software Package Data Exchange (SPDX) as its SBOM format of choice. All software produced from Microsoft will have an SPDX SBOM.

SBOMs provide two core benefits:

i) Software Transparency - this is a small step towards increasing trust as the SBOM describes the "ingredients" of the software and their relationships. This also enables external consumers of SBOMs to do vulnerability lookups on the open source software embedded within.
ii) File checksums for integrity verification purposes

## Goals
Primary goal is to generate SBOM for all the softwares produced by Microsoft. Here we are focusing on the following areas:

i) Staging pipeline 
ii) Arcade and all the repos that use arcade eg: Runtime, aspnetcore etc.
iii) Repos that are not on-boarded to arcade eg: arcade-services, OSOB, helix etc.

## Stakeholders
- .NET Core Engineering
- .NET Core Engingeering Partners
- US government

## Unknowns 
We are going to be focusing on generation of SBOM and archiving it, like we do for our manifest currently. Eventually SBOM will end up in a data base. We will know which service we will need to call to upload it to that db when we use the executable to generate SBOM. For now it is TBD. When we use the azure task, uploading is taken care of.

## Rollout and Deployment
- Firstly we will be generating SBOM for staging pipeline. Here we already have a place where we upload all the signed assets, so we will need to add a azure task to generate and upload the SBOM. After generating SBOM, we will need to get a sign off from partners to see if the generated SBOM is valid and contains all the assets.
- Then focus on our Engineering systems - In Arcade (main branch) we are planning to use the executable to generate SBOM. Here we will validate if SBOM is generated correctly.
- Backport SBOM generation changes to release-6.0 branch.
- The repos that use arcade may have multiple places where will have to generate SBOM. We will need to generate SBOM for all the repos that use arcade.There might be mutliple SBOM in this scenario. Then we need to get sign off from the repo owners to validate SBOM.
- Lastly, we will have to work on repos that are not on-boarded to arcade like arcade-service, helix, OSOB. 

## FR handoff 
- Will document any failures as I encounter. 
