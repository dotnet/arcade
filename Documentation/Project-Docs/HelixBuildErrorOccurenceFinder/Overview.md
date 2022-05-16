# ✔️ Introduction
## Requirements
We want to create a REST API that allows users to find the frequency of a specific error string in a repository's Helix build logs. So we want to be able to query an endpoint with a `repository`, `error_string`, `start_date`, and `end_date` to get a list of all the error occurrences of the string in the build logs matching those arguments.

# 🖥️ Implementation
## High-level diagram
<img width="3007" alt="Project diagram" src="https://user-images.githubusercontent.com/37950626/168691378-a0ad1daa-269b-483d-ab78-c2aae7d267be.png">

## Logic
1. Take and sanitize user input
2. Execute Kusto query (filter by repository, date_ranges, fail status)
3. Iterate through Kusto's results to retrieve log files' path in Azure Storage
4. Read log file's content line by line using file stream
5. Use string searching/matching to find error string in the lines.
6. If match is found, keep track of it and increment occurrences found.
7. Return the results found as a JSON object. 

### Kusto query
    Jobs
    | where Repository == "REPO_NAME"
    | project JobId, Repository, Properties
    | join kind=inner WorkItems on JobId
    | project JobId, JobName, Status, Started, Finished, ConsoleUri, QueueName, Repository, Properties
    | where Status == "Fail"
    | where Started between (datetime(YYYY-MM-DD) .. datetime(YYYY-MM-DD))

## Input
- Arguments
    - `repository`
    - `error_string`
    - `start_date`
    - `end_date`

- Constraints
    - `repository` must be an existing, public repository. Its spelling must match the repo name exactly.
    - `error_string` should probably have some kind of limit on length.
    - The duration between `start_date` and `end_date` should have a maximum of 12 months (?). If user input exceeds this value, one possible way of handling this is to just query jobs between 12 months before the given `end_date`.

## Dependencies
- Kusto
- Azure Account Storage

## String Matching
The three possible string matching methods ranked by speed/performance are:
1. C# `String.contains` 
2. Boyer-Moore string searching algorithm
3. Regex Class

This ranking is based on the following articles:

[Boyer-Moore VS String.contains](http://www.blackbeltcoder.com/Articles/algorithms/fast-text-search-with-boyer-moore)

**tldr**; Although Boyer-Moore is considered one of the fastest string-matching algorithms, C#'s `String.contains` method is faster as it uses assembly optimization. Although we might need a performance test since we will need to go line by line and load the strings from each log file if we use `String.contains`.

[String.contains VS Regex.isMatch](https://theburningmonk.com/2012/05/performance-test-string-contains-vs-string-indexof-vs-regex-ismatch/#:~:text=As%20you%20can%20see%2C%20Regex.IsMatch%20is%20by%20far,turned%20out%20to%20be%20significantly%20faster%20than%20String.IndexOf.)

**tldr**; Regex matching is way slower than String methods. It's only more useful if we want to pattern match as opposed to finding a fixed string.


## Output
#### Possible JSON output:
    {
        "repository": "",
        "error_string": "",
        "start_date": "",
        "end_date": "",
        "num_occurrences": 00,
        "failed_jobs: 
        [
            {
                "job_id": "",
                "job_name": "",
                "started": "",
                "finished": "",
                "queue_name": "",
                "properties" {}
            },
            {
                "job_id": "",
                "job_name": "",
                "started": "",
                "finished": "",
                "queue_name": "",
                "properties" {}
            },  
       ]
    } 
 
# 👓 Proof-of-Concept
- Console app
- Will test out string matching on a fixed number of log files first to see the speed of parsing a single log file.
- May also use POC to compare the performance of `String.contains` and Boyer-Moore.
