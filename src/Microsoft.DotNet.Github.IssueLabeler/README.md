# Microsoft.DotNet.Gitthub.IssueLabeler

It is web api which automatically labels an issue when it is opened in a Github repo. This tool is currently deployed as azure web service for the [corefx](https://github.com/dotnet/corefx.git) repo.

## How it works
The web api uses a pretrained [ML.NET](https://github.com/dotnet/machinelearning) model which is consumed through a nuget package Microsoft.DotNet.GitHubIssueLabeler.Assets. This model has been trained on around 10,000 issues already labeled in the corefx repo. The code for the model is available [here](https://github.com/dotnet/machinelearning-samples/tree/master/samples/csharp/end-to-end-apps/MulticlassClassification-GitHubLabeler).

Whenever an issue is opened in the repo, the web api receives the payload (using webhooks) containing all the information about the issue like title, body, milestone, issue number etc. It then supplies this information to already loaded pretrained model and the model predicts a probability distribution over the all possible labels. We then take the label with maximum probability and compare it with a threshold. if the predicted probability is greater than threshold then we apply the label otherwise we do nothing.

## How to deploy\maintain the web api
In order to deploy this web api, you need to publish it as an azure service. After publishing the app, you should enable the logging. This can be done in 2 ways
- You can add a web.config to your project. You require 
```<aspNetCore processPath=".\Microsoft.DotNet.Github.IssueLabeler.exe" stdoutLogEnabled="true" stdoutLogFile="\\?\%home%\LogFiles\stdout" />``` in your web.config file.

- You can enable logging by editing the web.config on your azure portal, created while publishing the web service.

After the logging has been enabled, the api will generate logs at the path supplied in your web.config file. All the lines which contain the issue number and predicted label (with confidence) start with #. You can use a regex to clean the log file.

## How to use this for other repos
In order to use this web api for any other github repo, you need to take the following actions :-

- Train a ML.NET model on the issues associated with that repo.
- Provide all the necessary information in the appsettings.json file eg GithubRepoName, GithubRepoOwner, Threshold etc
- Setup a [webhook](https://developer.github.com/webhooks/creating/) in the github repo with the listener address as ```serveraddress/api/WebhookIssue```