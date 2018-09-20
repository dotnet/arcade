# Onboarding onto Arcade
This is very much a WIP document still.  There are many details below that need to be filled in or clarified.  However, consider this the start.

## Enabling Dependency Flow
- Take a dependency on Arcade SDK  (this section will likely be broken out in the future versions of this doc)
  - Add entry in your global.json
  - Add Directory.Build
  - Copy 'eng' folder
  - Add the dotnetcore feed to netget.config
  - Add signtooldata.json
  - Make sure you have .sln file for repo
- Onboard official build to VSTS (enables push to BAR)
  - include base.yml so you have the right hooks
  - add join phase to yml
- Create version.details.xml (manual today)
- Create a channel mapping between branch and channel name
- Create subscription (manually using REST api)
