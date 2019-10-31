# Microsoft.DotNet.Helix.JobSender
This Package provides simple Helix Job sending functionality allowing sending jobs to helix with a minimal amount of C# code.

## Installing the Package

The Helix Packages are pushed to the dotnet-tools package feed:

> `https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-tools/nuget/v3/index.json`

To use these packages add the above feed to `NuGet.config`. There are currently no stable versions of the package in the feed so you must specify the version if you are using `dotnet add package`.

## Examples
All of the following examples are C# code that is inserted in the following template.
```csharp
using System;
using System.Threading.Tasks;
using Microsoft.DotNet.Helix.Client;

namespace Sample
{
  class Program
  {
    static async Task Main(string[] args)
    {
      var api = ApiFactory.GetAuthenticated(/* api access token here */);
      
      // Sample code goes here.
    }
  }
}

```

### Hello World
This will print out `'Hai Wurld!'` in the job console log.

```csharp
var job = await api.Job.Define()
  .WithType("test/helloworld")
  .WithTargetQueue("Windows.10.Amd64" /* Helix Queue ID Goes Here */)
  .WithSource("test/helloworld")
    .DefineWorkItem("Hello World")
    .WithCommand("echo 'Hai Wurld!'")
    .WithEmptyPayload()
    .AttachToJob()
  .WithCreator("john")
  .SendAsync();

Console.WriteLine($"Job '{job.CorrelationId}' created.");
```

### Using A Payload
Given a local text file `stuff.txt` this will print out the contents of that file in the job console log.

```csharp
var job = await api.Job.Define()
  .WithType("test/payload")
  .WithTargetQueue("Windows.10.Amd64" /* Helix Queue ID Goes Here */)
  .WithSource("test/payload")
    .DefineWorkItem("Using a Payload")
    .WithCommand("type stuff.txt")
    .WithFiles("stuff.txt")
    .AttachToJob()
  .SendAsync();

Console.WriteLine($"Job '{job.CorrelationId}' created.");
```

### Using a payload uri
This will print a report of the disk speed of the C: drive on the helix machine in the job console log.

```csharp
var job = await api.Job.Define()
  .WithType("test/diskspd")
  .WithTargetQueue("Windows.10.Amd64" /* Helix Queue ID Goes Here */)
  .WithSource("test/diskspd")
    .DefineWorkItem("Diskspd")
    .WithCommand("amd64\\diskspd.exe C:")
    .WithPayloadUri(new Uri("https://gallery.technet.microsoft.com/DiskSpd-A-Robust-Storage-6ef84e62/file/199535/1/DiskSpd-2.0.20a.zip"))
    .AttachToJob()
  .SendAsync();

Console.WriteLine($"Job '{job.CorrelationId}' created.");
```

