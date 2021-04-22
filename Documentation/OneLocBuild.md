# Localization with OneLocBuild in Arcade

As of April 1, 2021, all .NET repositories will be using OneLocBuild for localization. Documentation on this system can
be found [here](https://ceapex.visualstudio.com/CEINTL/_wiki/wikis/CEINTL.wiki/107/Localization-with-OneLocBuild-Task).
This system is **not a replacement for Xliff-Tasks**; rather, it is replacing the localization team's old system called
Simple Loc. Xliff-Tasks will continue to be used in addition to OneLocBuild.

To make OneLocBuild easier to use, we have integrated the task into Arcade. This integration is a job template
([here](/eng/common/templates/job/onelocbuild.yml)) that is described in this document.

## LocProject.json Index File

The core component of OneLocBuild is the LocProject.json file. This file is an index file containing references to
all of the files to localize in your repository. In order to reduce overhead for repo owners, we are auto-generating
the LocProject.json file (PowerShell script [here](/eng/common/generate-locproject.ps1)). This script and the
OneLocBuild Azure DevOps task are both wrapped in the onelocbuild.yml job template.

The script searches your checked-in code for all localized XLF files and template JSON files. Files can be excluded
using a checked in file (/Localize/LocExclusions.json). The LocExclusions file excludes files based on simple matching.
For example, the LocExclusions.json file below will exclude everything in a directory called `tests` and any file
which include `test.xlf` in its name.

```json
{
    "Exclusions": [
        "\\tests\\",
        "test.xlf"
    ]
}
```

The selected files are then added to a generated LocProject.json file. At this point, template currently provides two options
for how to proceed.

### Build-Time Generation

**The recommended path** is to have the script pass the generated LocProject.json directly to the OneLocBuild task.
This is the simpler of the two methods and removes the overhead of needing to maintain a checked in
LocProject.json file. The LocProject.json file is emitted in build logs and as a build artifact for examination.

### Build-Time Validation

While it is **not the recommended path**, repos can instead opt to check in a static LocProject.json and have the
script compare it against the generated one. If they differ, the script will break the build so that a dev can
update either the LocProject.json or the LocExclusions.json file accordingly.

Because the script can be run locally, devs can also do this validation prior to pushing their changes.

### Custom LocProject.json Files

Currently, the LocProject.json generation script only creates fairly uniform LocProject.json files. If your repository
requires the use of any of the more complex LocProject.json features as described in the OneLocBuild docs linked above,
the OneLocBuild template in this doc will not work and you will need to check in and maintain the LocProject.json file
manually.

## OneLocBuild Template Parameters

The most basic structure for calling the OneLocBuild template is:

```yaml
jobs:
- template: /eng/common/templates/job/onelocbuild.yml
  parameters:
    LclSource: lclFilesfromPackage
    LclPackageId: 'LCL-PACKAGE-ID'
```

The parameters that can be passed to the template are as follows:

| **Parameter** | **Default Value** | **Notes** |
|:-:|:-:|-|
| `RepoType` | `'gitHub'` | Should be set to `'gitHub'` for GitHub-based repositories and `'azureDevOps'` for Azure DevOps-based ones. |
| `SourcesDirectory` | `$(Build.SourcesDirectory)` | This is the root directory for your repository source code. |
| `CreatePr` | `true` | When set to `true`, instructs the OneLocBuild task to make a PR back to the source repository containing the localized files. |
| `AutoCompletePr` | `false` | When set to `true`, instructs the OneLocBuild task to autocomplete the created PR. Requires permissions to bypass any checks on the main branch. |
| `UseCheckedInLocProjectJson` | `false` | When set to `true`, instructs the LocProject.json generation script to use build-time validation rather than build-time generation, as described above. |
| `LanguageSet` | `VS_Main_Languages` | This defines the `LanguageSet` of the LocProject.json as described in the [OneLocBuild task documentation](https://ceapex.visualstudio.com/CEINTL/_wiki/wikis/CEINTL.wiki/107/Localization-with-OneLocBuild-Task?anchor=languageset%2C-languages-(required)). |
| `LclSource` | `LclFilesInRepo` | This passes the `LclSource` input to the OneLocBuild task as described in [its documentation](https://ceapex.visualstudio.com/CEINTL/_wiki/wikis/CEINTL.wiki/107/Localization-with-OneLocBuild-Task?anchor=languageset%2C-languages-(required)). For most repos, this should be set to `LclFilesfromPackage`. |
| `LclPackageId` | `''` | When `LclSource` is set to `LclFilesfromPackage`, this passes in the package ID as described in the [OneLocBuild task documentation](https://ceapex.visualstudio.com/CEINTL/_wiki/wikis/CEINTL.wiki/107/Localization-with-OneLocBuild-Task?anchor=scenario-2%3A-lcl-files-from-a-package). |
| `condition` | `''` | Allows for conditionalizing the template's steps on build-time variables. |

It is recommended that you set `LclSource` and `LclPackageId` as shown in the example above.