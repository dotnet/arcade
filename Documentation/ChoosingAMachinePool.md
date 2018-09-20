# Choosing a Machine Pool


## Builds
All Azure Pipelines builds should use the following helix-managed agent queues
* Pull Request validation and Public CI
  * [dotnet-external-temp]
* Official Signed Builds
  * [dotnet-internal-temp]
  * [dnceng-linux-internal-temp]

## Test Execution
All test execution should run through helix. An up to date list of helix queues can be obtained from the [Helix Queue Info Api].

The definitions for all helix-managed machines are located in the [dotnet-helix-machines] repo.
The readme in [dotnet-helix-machines] contains information on where machine setup information lives and how to change it if needed.

[dotnet-internal-temp]: https://dnceng.visualstudio.com/internal/_settings/agentqueues?queueId=67&_a=agents
[dnceng-linux-internal-temp]: https://dev.azure.com/dnceng/internal/_settings/agentqueues?queueId=61&_a=agents
[dotnet-external-temp]: https://dev.azure.com/dnceng/internal/_settings/agentqueues?queueId=47&_a=agents

[dotnet-helix-machines]: https://dev.azure.com/dnceng/internal/internal%20Team/_git/dotnet-helix-machines?path=%2FREADME.md&version=GBmaster
[Helix Queue Info Api]: https://helix.dot.net/swagger/ui/index#!/Information/Information_QueueInfoList
