## Overall Principles
- Documentation is something that we want available to our team and customers for the services that we create and support 
- Documentation should be easy to find and follow
   - Do not let a lack of clarity on where to place a document stop you from creating the document – we can always relocate it later. 
- While we do have multiple repositories of documentation, the base document should only be in a single location (see guidance below) with links to the other locations. 
- It is everyone’s responsibility to make sure our documentation stays up to date and usable
- Our internal tools/processes should have the same level of documentation as those used by our customers

## Definition of Documentation types
- Document – Principles about the decisions that were made to implement deliverables for an epic
- Wiki – Operations aspects of a service, help with date to date work

## Where to put your Document
- Readme files related to a tool/repo
  - Should be linked to [.NET Eng Services Readme Dashboard](https://dev.azure.com/dnceng/internal/_wiki/wikis/DNCEng%20Services%20Wiki/891/NET-Eng-Services-Readme-Dashboard) to simplify discoverability
- [AzDo Wiki Page](https://dev.azure.com/dnceng/internal/_wiki/wikis/DNCEng%20Services%20Wiki/889/Home) 
   - Main landing area for operational wikis
   - Structure of files
      - Flat structure with some dashboards to help us organize information around a common theme
- [Arcade github repo](https://github.com/dotnet/arcade)
   - Main repository for our documents
   - Structure of files
      - All files should be organized into a single parent folder per epic within the documentation folder. 
      - Parent folder should be named related to Epic it references and should be created when the epic is funded
- [Arcade github wiki](https://github.com/dotnet/arcade/wiki)
   - Information that we want to be publicly available 
   - Pages with links back to the AzDo Wiki page 
- [Microsoft SharePoint Site](https://microsoft.sharepoint.com/teams/netfx/engineering/Shared%20Documents/Forms/AllItems.aspx)
   - Main repository for documents related to security as well as anything else that we determine as not for public consumption
   - Structure of files should be similar to the Arcade github repo (see above)
