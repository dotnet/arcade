# Microsoft.DotNet.Build.Tasks.Installers

Task package for installer specific tasks. Currently contains:
- **CreateLightCommandPackageDrop** - Create a layout that can be used to
  re-execute a light command. This can be used during post-build signing
  after files have been replaced. A cmd file that can be used to reconstruct the installer is created.
  
  The parameters for this task should be set to the parameters used on the input light command.
  
  After creating the layout, it can be zipped and added to the build artifacts.
  
  Parameters:
  - LightCommandWorkingDir - Base directory to place the layout
  - Out - Original installer output file. The filename is stripped
    of its extension and appended to LightCommandWorkingDir as the target directory for the layout
  - NoLogo - Add `-nologo`
  - Fv - Add `-fv`
  - PdbOut - Add `-pdbout <pdbfile>`
  - Cultures - Add `-cultures:<list of cultures>`
  - WixProjectFile - Add `-wixprojectfile:<project file>`
  - ContentsFile - Add `-contentsfile <contents file>`
  - OutputsFile - Add `-outputsfile <outputfile file>`
  - BuiltOutputsFile - Add `-builtoutputsfile <outputfile file>`
  - Loc - Add `-loc:<loc files>` and copy the loc files to the layout
  - Sice - Add `-sice:<supressed consistency checks>`
  - WixExtensions - Add `-ext <extension>` for each extension
  - WixSrcFiles - Add each input source file