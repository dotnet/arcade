# Signing as Part of Publishing

Any repositories using V3 publishing, Publish.proj, and the Arcade Shared Framework SDK have signing enabled by default and no further work is needed. Ensure that any other signing steps are disabled and that any repo specific publishing steps have been added after signing is complete. More information about the file types that are signed and the certificates used to sign them can be found here:
https://github.com/dotnet/arcade/blob/master/src/Microsoft.DotNet.Arcade.Sdk/tools/Sign.props

A manifest file is created as part of the build process, it can be found in AssetManifests/Manifest.xml. This file lists everything that will be signed once the build is complete. If this file has been created and the contents match the expected set of signed files for the build then no further action is needed.

While most repositories won't need to take any action to have signing work correctly, signing settings can be customized by added a Signing.props file to the /eng folder and adding defintions specific to the repository requirements.

This is the basic schema for signing.props, most repositories won't need anything beyond this:

```
<Project>
    <ItemGroup>
        <!-- Use this format to identify files that should be signed with a non-default certificate-->
        <FileSignInfo Include="FileToSign.dll" CertificateName="NameToSignWith" />
    </ItemGroup>
</Project>
```

This is the slightly more complex sample schema for the signing.props file:

```
<Project>
	<PropertyGroup>
		<AllowEmptySignList Condition="PutConditionStatementHere">true</AllowEmptySignList>
	</PropertyGroup>
	
	<Target Name="PrepareItemsToSign" BeforeTargers="Sign">
		<ItemGroup>
		<!--
          Replace the default items to sign with the specific set you want. This allows the build to call
          Arcade's Sign.proj multiple times for different sets of files as the build progresses.
        -->
			<ItemsToSign Remove="@(ItemsToSign)" />
			
			<!-- Exclude files that are not signed -->
			<FileSignInfo Include="dontSignMe.exe;dontSignEither.dll" CertificateName="None" />	
		</ItemGroup>
		
		<ItemGroup Condition="WhenToSignTheseFiles">
			<ItemsToSign Include"$(ArtifactPathDir)**/*.filetype"/>
		</ItemGroup>
		
		<ItemGroup>
			<!-- External files -->
			<ItemsToSign Remove="@(ItemsToSign->WithMetadataValue('Filename', 'Newtonsoft.Json'))" />
		</ItemGroup>

		<ItemGroup>
			<ItemsToSign Update="@(ItemsToSign)" Authenticode="$(CertificateId)" />
		</ItemGroup>
	</Target>
	
	<!-- Assign specific file types to specific certificates-->
	<ItemGroup>
        <FileExtensionSignInfo Include=".msi" CertificateName="Microsoft400" />
        <FileExtensionSignInfo Include=".pkg" CertificateName="8003" />
        <FileExtensionSignInfo Include=".deb;.rpm" CertificateName="LinuxSign" />
    </ItemGroup>
</Project>
```

Repositories that are not using the Arcade Shared Framework SDK will need to add the CreateLightCommandPackageDrop task to the wix.targets file that creates MSIs for signing. See the Arcade Shared Framework SDK repo for an example.
https://github.com/dotnet/arcade/tree/master/src/Microsoft.DotNet.Build.Tasks.Installers
https://github.com/dotnet/arcade/blob/master/src/Microsoft.DotNet.Build.Tasks.SharedFramework.Sdk/targets/windows/wix.targets

Additional information about the publishing process in general can be found here:
https://github.com/dotnet/arcade/blob/master/Documentation/CorePackages/Publishing.md

Additional information about the legacy signing process can be found here:
https://github.com/dotnet/arcade/blob/master/Documentation/CorePackages/Signing.md