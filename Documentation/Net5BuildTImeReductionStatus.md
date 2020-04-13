# .NET 5 Build Time Reduction Status

## Budgets

The expected potential critical paths of the build after consolidation is complete are:

- runtime -> aspnetcore -> sdk -> installer
- runtime -> winforms -> wpf -> windowsdesktop -> sdk -> installer
- runtime -> wpf-int -> wpf -> windowsdesktop -> sdk -> installer

Based on the rough amount of build work done in each repo, budgets for each build leg, to fit within the overall 2 hour official time goal, have been set as follows:

| Repo                                 | Budget (mins) |
| ------------------------------------ | ------------- |
| runtime                              | 45            |
| aspnetcore                           | 45            |
| sdk + installer                      | 30            |
| winforms + wpf + windowsdesktop (wd) | 45            |
| wpf-int + wpf + windowsdesktop (wd)  | 45            |

## Current Status

- **NA = Not applicable** - Not applicable to this repo
- **NYA = Not yet applicable** - Requires work from Eng teams before implementation is possible
- **NR = Not required** - This work is not necessarily required to meet the 2 hour build time. For instance,
  repos that are disconnecting from the main flow and shipping only as disconnected components may choose to keep
  signing and validation within their builds.

*Note that if some fields are NYA (signing/validation), the "Within budget" field is a rough estimation
of whether the repo will be within budge once those items are complete.*


| Repo               | Owner    | Consolidated/Removed from flow       | Moved tests                          | Post-signing | Post-validation | Budget (mins)                   | Within budget                     |
| ------------------ | -------- | ------------------------------------ | ------------------------------------ | ------------ | --------------- | ------------------------------- | --------------------------------- |
| websdk             | vramak   | <span style="color:red">No</span>    | NA                                   | NA           | NA              | NA                              | NA                                |
| aspnetcore         | kevinpi  | NA                                   | <span style="color:red">No</span>    | NYA          | NYA             | 45                              | <span style="color:red">No</span> |
| aspnetcore-tooling | kevinpi  | <span style="color:red">No</span>    | NA                                   | NA           | NA              | NA                              | NA                                |
| efcore             | kevinpi  | <span style="color:green">Yes</span> | <span style="color:green">No</span>  | NR           | NR              | NA                              | NA                                |
| extensions         | kevinpi  | <span style="color:red">No</span>    | <span style="color:red">No</span>    | NR           | NR              | NA                              | NA                                |
| installer          | dondr    | NA                                   | <span style="color:red">No</span>    | NYA          | NYA             | 30 (w/sdk)                      | <span style="color:red">No</span> |
| runtime            | jaredpar | <span style="color:green">Yes</span> | <span style="color:green">Yes</span> | NYA          | NYA             | 45                              | <span style="color:red">No</span> |
| sdk                | dondr    | NA                                   | <span style="color:green">Yes</span> | NYA          | NYA             | 30 (w/installer)                | <span style="color:red">No</span> |
| templating         | joaguila | NA                                   | <span style="color:red">No</span>    | NYA          | NYA             | NA                              | NA                                |
| windowsdesktop     | srivatsm | NA                                   | <span style="color:red">No</span>    | NYA          | NYA             | 45 (w/winforms + wpf)           | <span style="color:red">No</span> |
| winforms           | srivatsm | NA                                   | <span style="color:red">No</span>    | NYA          | NYA             | 45 (w/wpf + wd)                 | <span style="color:red">No</span> |
| wpf-int            | srivatsm | NA                                   | <span style="color:red">No</span>    | NYA          | NYA             | 45 (w/wpf + wd)                 | <span style="color:red">No</span> |
| wpf                | srivatsm | NA                                   | <span style="color:red">No</span>    | NYA          | NYA             | 45 (w/winforms or wpf-int + wd) | <span style="color:red">No</span> |