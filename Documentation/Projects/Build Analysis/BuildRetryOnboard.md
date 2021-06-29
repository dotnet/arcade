# Build Retry

## How it works?
![](./Resources/BuildRetryWorkflow.png?raw=true)

The build retry feature is composed of two parts.

The first one is having the build configuration file as part of the build artifacts. In order to have this in place it is necessary to follow the [onboarding process](#how-to-onboard).

The second part is what is done by the Build Result Analysis. When the build finishes, it starts getting processed. If it failed, the build is analyzed to see if it can be retried. In order to be retried, at least one of the rules specified in a build configuration file needs to be met. 
## How to onboard?
1. In order to use the Build Retry feature it's necessary to have the `.NET Helix` GitHub application installed in the repo in which you intend to use the feature. </br>
If you want to get the application installed, you can contact the [.NET Core Engineering Services team](https://github.com/dotnet/core-eng/wiki/How-to-get-a-hold-of-Engineering-Servicing)


1. Create the [build configuration file](#Build-configuration-file-structure). The name of the file should be: `build-configuration.json` and you can create the file inside any folder. That folder is the one that you are going to publish. </br>
Ex. \eng\BuildConfiguration\build-configuration.json
1. [Publish an artifact having the `build-configuration.json` file to Azure Pipeline](https://docs.microsoft.com/en-us/azure/devops/pipelines/artifacts/pipeline-artifacts) in the repo that you are expecting to get retry. The name of the artifact should be: `BuildConfiguration`

	Ex.
	``` 
	- publish: $(Build.SourcesDirectory)\eng\BuildConfiguration
	  artifact: BuildConfiguration
	``` 

## Build configuration file structure
```json 
{
   "RetryCountLimit":1,
   "RetryByAnyError":false,
   "RetryByErrors":[
      {
         "ErrorRegex":"Regex"
      }
   ],
   "RetryByPipeline":{
      "RetryJobs":[
         {
            "JobName":"JobName"
         }
      ],
      "RetryStages":[
         {
            "StageName":"StageName"
         }
      ]
   }
}
```

- **RetryCountLimit:** Number of retries that can be done on your build. <br/> 
Ex. "RetryCountLimit": 1 means is going to be retried 1 time by the Build Result Analysis giving a total of 2 executions. <br> 
Default value: 0. Max internal value: 10.

- **RetryByAnyError:** The build is going to be retried independently of which was the reason of the failure. <br> 
Default value: False. 

- **RetryByErrors:** List of 'ErrorRegex' (.NET flavor) that will look for a match on the build pipeline errors. In case there is an error matching the regex the build is going to be retried.

	Ex.<br/>
	In order to retry a build with the following pipeline errors
	![](./Resources/PipelineErrorsExample.png?raw=true)
	you could have a file with the following information:
	```json 
    {
       "RetryCountLimit":1,
       "RetryByErrors":[
          {
             "ErrorRegex":"Vstest failed with error.*"
          }
       ]
    }
	```

- **RetryByPipeline:** The retry by pipeline is expecting a list of Jobs or/and Stages names which in case on fail should cause a retry by the Build Result Analysis.

	The job names reference are the ones on the pipeline:<br/>
	Ex. 
	![](./Resources/JobNameErrorsExample.png?raw=true)
	```json 
    {
       "RetryCountLimit":1,
       "RetryByPipeline":{
          "RetryJobs":[
             {
                "JobName":"Validate_Test_Failing"
             }
          ]
       }
    }
	```
	The stage name references are the ones on the yaml </br>
	Ex.
	![](./Resources/StageNameExample.png?raw=true)
	```json
    {
       "RetryCountLimit":1,
       "RetryByPipeline":{
          "RetryStages":[
             {
                "StageName":"Build"
             }
          ]
        }
    }
	```
