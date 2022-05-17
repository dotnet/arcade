# ✔️ Introduction
## Requirements
We want to create a REST API that allows users to find the frequency of a specific error string in a repository's Helix test logs. So we want to be able to query an endpoint with a `repository`, `error_string`, `start_date`, and `end_date` to get a list of all the error occurrences of the string in the build logs matching those arguments.

# 🖥️ Implementation
## High-level diagram
<img width="3007" alt="Project diagram" src="./Resources/ProjectDiagram.png">

## Logic
1. Take and sanitize user input
2. Execute Kusto query (filter by repository, date_ranges, fail status)
3. Iterate through Kusto's results to retrieve log files' path in Azure Storage
4. Read log file's content line by line using file stream
5. Use string searching/matching to find error string in the lines.
6. If match is found, keep track of it and increment occurrences found.
7. Return the results found as a JSON object. 

### Kusto query
    let RepoJobs = Jobs
    | where Repository == "REPO_NAME"
    | project JobId;
    WorkItems
    | where JobId in (RepoJobs)
    | project JobId, JobName, Status, Started, Finished, ConsoleUri, QueueName
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
    - The duration between `start_date` and `end_date` should have a maximum of ~~12~~ 3 months (?). If user input exceeds this value, one possible way of handling this is to just query jobs between our defined max number of months before the given `end_date` and alert the user that this was done instead of their original query.

## Dependencies
- Kusto
- Azure Account Storage

## String Matching
The three possible string matching methods ranked by speed/performance are:
1. C# `String.contains`, `String.replace`, etc
2. Boyer-Moore string searching algorithm
3. Regex

This ranking is based on the following articles:

[Boyer-Moore VS String.contains](http://www.blackbeltcoder.com/Articles/algorithms/fast-text-search-with-boyer-moore)

**tldr**; Although Boyer-Moore is considered one of the fastest string-matching algorithms, C#'s `String.contains` method is faster as it uses assembly optimization. Although we might need a performance test since we will need to go line by line and load the strings from each log file if we use `String.contains` and that might take even longer.

[String.contains VS Regex.isMatch](https://theburningmonk.com/2012/05/performance-test-string-contains-vs-string-indexof-vs-regex-ismatch/#:~:text=As%20you%20can%20see%2C%20Regex.IsMatch%20is%20by%20far,turned%20out%20to%20be%20significantly%20faster%20than%20String.IndexOf.)

**tldr**; Regex matching is way slower than String methods. It's only more useful if we want to pattern match as opposed to finding a fixed string. (Actually this raises the question - do we want to pattern match?)

Also, [this article](https://cc.davelozinski.com/c-sharp/c-net-fastest-way-count-substring-occurrences-string) compares the speeds of different methods of counting substring occurences.

**tldr**; Using the basic counting code below was the fastest method even over very long strings. It also corroborates the article saying Regex matching is very slow for long strings.


    for (int y = 0; y &lt; sf.Length; y++)
    {
        c[y] += (ss[x].Length - ss[x].Replace(sf[y], String.Empty).Length) / sf[y].Length;
    }


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
