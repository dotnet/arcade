# Creating Azure Boards WorkItems

For some issues, we will need to open Azure Boards workitems, rather than github issues. These include:

* Security related changes (see [issue tracking guidance](IssueTrackingGuidance.md))
* Any work for non-open source projects

In these instances, we still want to have issues, and we still want to link them to Epics that are in arcade.

## Opening an Azure Boards WorkItem

* WorkItems should be opened in the [internal Azure DevOps project](https://dev.azure.com/dnceng/internal/_workitems/)
* When creating a new work item, create the work item as a [task](https://dev.azure.com/dnceng/internal/_workitems/create/Task)
* Set Area to internal\Dotnet-Core-Engineering
* Give it a meaningful title and description
* Update the GitHub Friendly Title and GitHub Friendly Description with information that can be shared on GitHub (in the public).
* Add a link to the GitHub epic in the Epic Issue field.
* Add any tags that you want your GitHub issue to be labeled with (i.e. First Responder if the issue is a First Responder issue)
* An issue linking your newly created Azure Boards work item will be created and added to the Projects (beta) board
    * The epic issue field will be filled out if the GitHub link was supplied
    * If the GitHub Friendly Title is set, the created github issue will use it. Otherwise, will use "Azure Boards Issue #[Issue Number]".
    * If the GitHub Friendly Description is set, it will be used and a link to the Azure Boards work item will be added. Otherwise, the issue will only have a link to the Azure Boards work item.
    * If any tags have been set, it will label the issue with those tags
