<!-- Copyright (c) .NET Foundation and contributors. All rights reserved. Licensed under the MIT license. See License.txt in the project root for full license information. -->
<Project>
  <Target Name="GenerateWixpackPackage" AfterTargets="CoreCompile">
    <PropertyGroup>
      <WixpackWorkingDir>$(IntermediateOutputPath)wixpack</WixpackWorkingDir>
      <WixpackOutputDir>$(ArtifactsNonShippingPackagesDir)</WixpackOutputDir>
    </PropertyGroup>

    <CreateWixBuildWixpack
      BindTrackingFile="$(IntermediateOutputPath)$(BindTrackingFilePrefix)%(CultureGroup.OutputSuffix)$(BindTrackingFileExtension)"
      Cultures="%(CultureGroup.Identity)"
      DefineConstants="$(DefineConstants);$(SolutionDefineConstants);$(ProjectDefineConstants);$(ProjectReferenceDefineConstants)"
      Extensions="@(_ResolvedWixExtensionPaths)"
      InstallerPlatform="$(InstallerPlatform)"
      InstallerFile="$(IntermediateOutputPath)%(CultureGroup.OutputFolder)$(TargetFileName)"
      IntermediateDirectory="$(IntermediateOutputPath)%(CultureGroup.OutputFolder)"
      OutputFolder="$(WixpackOutputDir)"
      OutputType="$(OutputType)"
      PdbFile="$(IntermediateOutputPath)%(CultureGroup.OutputFolder)$(TargetPdbFileName)"
      PdbType="$(DebugType)"
      SourceFiles="@(Compile)"
      WixpackWorkingDir="$(WixpackWorkingDir)">
      <Output TaskParameter="OutputFile" PropertyName="_WixBuildCommandPackageNameOutput" />
    </CreateWixBuildWixpack>
  </Target>
</Project>
