# Native Dependency Blob Storage Format

This document is intended to be a reference for how we are storing native toolset dependencies such that they can be accessed and utilized in a consistent well-known manner.

## Blob storage layout

```Text
\nativeassets
  - [flat file drop of zips, tarballs, etc...]
\installers
  \[api version]
    \[tool name]
      \[version]
        \[OS]
          \[arch]
            -install.[extension] # installer
            -[toolname].[extension] # shim
```

### nativeassets

The `nativeassets` folder is a flat file dump of all dependencies.  This is assuming that the tools are distributed with versioned filenames.

### installers

The `installers` folder is the common entry point that is used to acquire native toolset dependencies.  It contains both an "installer" file and shim(s) for using the native tool.

The "installer" is responsible for downloading the native asset and laying it out in the folder specified.  It will also download any shims required for using the native asset.

**Why is each native dependency required to have an "installer", why isn't the local repo handling unzipping and laying out the assets?**

I think that this model will allow us to be a bit more flexible in the types of dependencies that we install and provide a method for non-xcopy deployable dependencies to be installed in the future.

**Is maintaining installers going to be a lot of maintenance?**

I don't **think** so.  It should be a one-time cost for each asset and adding a new dependency should largely be a copy-paste type update with some version number updates.  If we decide to largely break away from how we layout assets or something else, we can use the "api version" to version these changes.

### shims

"Shims" are used to execute the native toolset.  Since there is no enforced structure on the file layout for a native asset, shims bridge the gap so that we have a single well-known entry point that can be used for our native toolset.  Having shims allows us to put all of them in a single folder (in a given repo) so that we can use them to access the tools rather than managing path access to every known toolset.

### api version

The "api version" should correspond to the version of the tool that each repo uses to acquire native toolset dependencies.  It is the *hope* that this version rarely (never?) change.

## Example - Azure blob storage container

```Text
\nativeassets
  -cmake-3.10.3-win32-x86.zip
  -cmake-3.10.3-win64-x64.zip
  -cmake-3.11.1-win32-x86.zip
  -cmake-3.11.1-win64-x64.zip
  -python-3.6.5-embed-amd64.zip
  -python-3.7.0b3-embed-win32.zip
\installers
  \1.0
    \cmake
      \3.10.3
        \Windows
          \x64
            -install.ps1
            -cmake.bat
        \Linux
          \x64
            -install.sh
            -cmake.sh
        \OSX
          \x64
            -install.sh
            -cmake.sh
```
