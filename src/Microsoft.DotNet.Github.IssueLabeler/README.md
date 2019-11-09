# Microsoft.DotNet.Gitthub.IssueLabeler

It is web api which automatically labels an issue when it is opened in a Github repo. This tool is currently deployed as azure web service for the [corefx](https://github.com/dotnet/corefx.git) repo.

## How it works
The web api uses a pretrained [ML.NET](https://github.com/dotnet/machinelearning) model which is consumed through a nuget package Microsoft.DotNet.GitHubIssueLabeler.Assets. This model has been trained on around 15,000 issues, and 10,000 PRs already labeled in the corefx repo. To see a simple end-to-end machine learning sample for how to create a model, you can check [here](https://github.com/dotnet/machinelearning-samples/tree/master/samples/csharp/end-to-end-apps/MulticlassClassification-GitHubLabeler).

Whenever an issue is opened in the repo, the web api receives the payload (using webhooks) containing all the information about the issue like title, body, milestone, issue number etc. It then supplies this information to already loaded pretrained model and the model predicts a probability distribution over the all possible labels. We then take the label with maximum probability and compare it with a threshold. if the predicted probability is greater than threshold then we apply the label otherwise we do nothing. We use a separate model for predicting label for pull requests, since PRs contain extra information through their file diffs. 

## How to deploy\maintain the web api
In order to deploy this web api, you need to publish it as an azure service. After publishing the app, you should enable the logging. This can be done in 2 ways
- You can add a web.config to your project. You require 
```<aspNetCore processPath=".\Microsoft.DotNet.Github.IssueLabeler.exe" stdoutLogEnabled="true" stdoutLogFile="\\?\%home%\LogFiles\stdout" />``` in your web.config file.

- You can enable logging by editing the web.config on your azure portal, created while publishing the web service.

After the logging has been enabled, the api will generate logs at the path supplied in your web.config file. All the lines which contain the issue number and predicted label (with confidence) start with #. You can use a regex to clean the log file.

## How to use this for other repos
In order to use this web api for any other github repo, you need to take the following actions :

- Train a ML.NET model on the issues and pull requests associated with that repo.
- Provide all the necessary information in the appsettings.json file eg GithubRepoName, GithubRepoOwner, Threshold etc
- Setup a [webhook](https://developer.github.com/webhooks/creating/) in the github repo with the listener address as ```serveraddress/api/WebhookIssue```

## How to get started with training models for other repositories
This project has been made generic enough to help with creating issue labelers for any repostory. The first step towards getting started with this application is to use the GithubIssueDownloader class to download all existing labels in a given repository. The GitHubIssueDownloader generates a dense dataset containing IDs for both issues and pull requests.

Once the details for all the labeled issues and pull requests in a repo finished downloading, then we can use APIs provided in DatasetHelper to prepare the datasets with proper format for training using ML.NET. The recommended approach is to separate the downloaded dataset into a PR dataset and an issue dataset. Depending on the nature of your repository you may decide to have columns in the training dataset that area slightly different from what is assumed. You may in fact decide to play around with different column values until you come up with final PR and Issue models that result in the best accuracy.

You may in fact decide that what works best for you is keeping a single combined trained model for both issues and pull requests. The DatasetHelper APIs could be a good way for helping you try different types of models until you finalize your choice for deployment.

### How to get separate training datasets for labeling issues and pull requests: 
As mentioned earlier you can choose to keep the mixed dataset containing both issues and pull requests, downloaded from GitHubIssueDownloader, or to customize it further using additional APIs provided in DatasetHelper class. The DataHelper class uses existing downloaded information from GitHubIssueDownloader to add or remove columns. So far, by trial and error we have found certain columns help drastically improve the accuracy of the issue labeler and we summarized them in the following API:

```C#
datasetHelper.AddOrRemoveColumnsPriorToTraining("GitHubIssueDownloaderFormat.tsv", "ready-to-train-with-both-issuesAndPrs.tsv");
```
This API currently tries to add columns for github user @ mentions and file information on pull requests into separate columns, all from existing columns inside `GitHubIssueDownloaderFormat.tsv`. Since the accuracy of a model has a direct correlation with which type of columns it received, we want to be careful with providing well structured columns into a dataset for training.

In order to prepare a dataset with only issues, you can further reduce the output by filtering with `DatasetHelper.OnlyIssues` API.
However, for pull requests it is important to use the `includeFileColumns` as shown below before calling the `OnlyPrs`:
```C#
datasetHelper.AddOrRemoveColumnsPriorToTraining("GitHubIssueDownloaderFormat.tsv", "both-issuesAndPrs-includeFileColumns.tsv", includeFileColumns: true);
datasetHelper.OnlyPrs("both-issuesAndPrs-includeFileColumns.tsv", "only-prs.tsv");
``` 
Remember, that preparing your dataset for training a model consists of two stages: (1) downloading all the information you need from github using the GithubIssueDownloader into a file that is your reference dataset, and (2) post-processing the downloaded information into multiple new columns, each conveying a certain kind of information for the training stage. The more we know about how files and user mentions map into labels in a repository (summarized into separate columns), the better the resulting accuracy of the training model with become. 

This shows how/why a single repository could have multiple different kinds of issue labeler models. Each using dataset with different set of additional columns, each aiming to help further help machine learning code more efficient predict labels. As you would expect, the columns expected in a dataset for a performance label prediction, is different from what we expect to have in a dataset that predicts "area-" labels. Feel free to further customize this API with the specific needs of the labels in your own repository.

Once you have a dataset with any of the above formats, you would then want to break your dataset down into three segments: training data (first 80%), validation data (second 10%), and test data (remaining 10%). You can use the `DatasetHelper.BreakIntoTrainValidateTestDatasets(..)` API to get this segmentation.