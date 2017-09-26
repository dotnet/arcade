# Secure Transport Feeds

## Implementation:
We make use of an Azure Function Proxy to redirect nuget requests to the appropriate blob resources.

## Documentation to create a secure transport feed:
1. Create the storage account and container you would like to use

2. Create a SAS token for the container (this is done via Azure Portal - under your storage account, there is a Shared Access Signature option)

3. Configure and generate a SAS token in the following manner.
![](./SAS.PNG?raw=true)

4. Setup the proxy (contact dotnetes if you are unfamiliar with the portal)
  
  - Set up an Azure Function with the name "STORAGEACCOUNT-translate"
  - Enable Function proxies under the Function app settings tab
  - Edit the host.json and save
     ```
     {
        "$schema": "http://json.schemastore.org/proxies",
        "proxies": {
            "<STORAGE ACCOUNT>-translate-proxy": {
                "matchCondition": {
                    "route": "/sv/{svParam}/sr/{srParam}/sig/{sigParam}/se/{seParam}/sp/{spParam}/{*restOfPath}"
                },
                "backendUri": "https://<STORAGE ACCOUNT>.blob.core.windows.net/{restOfPath}",
                "requestOverrides": {
                    "backend.request.method": "get",
                    "backend.request.querystring.sv": "{svParam}",
                    "backend.request.querystring.sr": "{srParam}",
                    "backend.request.querystring.sig": "{sigParam}%3D",
                    "backend.request.querystring.se": "{seParam}T07%3A00%3A00Z",
                    "backend.request.querystring.sp": "{spParam}"
                }
            }
        }
    }
     ```
5. In your publish step in the build, run the PushToBlobFeed task with secure settings
```
<Project ToolsVersion="12.0" DefaultTargets="PublishPackages" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <UsingTask TaskName="PushToBlobFeed" AssemblyFile="Microsoft.DotNet.Build.Tasks.Feed.dll"/>
  <PropertyGroup>
    <PackagesPattern>D:\Scratch\TestTF\Override\**/*.nupkg</PackagesPattern>
  </PropertyGroup>
    
  <Target Name="PublishPackages" >
    <PropertyGroup>
      <RelativePath>packages</RelativePath>
    </PropertyGroup>
    <ItemGroup>
      <ItemsToPush Include="$(PackagesPattern)"/>
    </ItemGroup>
    <PushToBlobFeed AccountName="$(AccountName)"
                  AccountKey="$(AccountKey)"
                  ContainerName="$(ContainerName)"
                  ItemsToPush="@(ItemsToPush)"
                  RelativePath="$(RelativePath)"
                  PublishFlatContainer="$(PublishFlatContainer)"
                  Overwrite="$(Overwrite)" 
                  <!-- PASS IN THE FOLLOWING PROPERTIES FOR SECURE FEEDS -->
                  IsSecure="true" 
                  SASToken="<GENERATED SAS TOKEN>" 
                  />
  </Target>
</Project>
```
