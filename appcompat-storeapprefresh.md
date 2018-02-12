# Refresh of apps from the store

We should regularly update apps from the store, to avoid running stale/broken apps and to pick up the new apps which are likely targeting newer toolchain versions and newer features.
Currently this process is mostly manual and this attempts to describe what needs to be done.

Last time the process has been done by Vitek Karas <Vitek.Karas@microsoft.com> and Ravi Eda <Ravi.Eda@microsoft.com>. The data/tools/logs from the last attempt are stored here: `\\fxcore\apps\WindowsStore\UWP\dec17`.

## Getting apps from the store
We need somebody from the store team to get us the list of the apps along with some download links. We also need to get the ranking information for these apps. Last time we got this list from Wei Wang (OSG-DCE) <weiwa@microsoft.com> and from Zach Capehart <zacape@microsoft.com> (possibly two different teams, but both can provide the list). Other people involved in the email discussion on this (for future reference): Navit Saxena <navits@microsoft.com>, Brandon Wang <Brandon.Wang@microsoft.com> and Cliff Strom <Cliff.Strom@microsoft.com>. From our side Matthew Whilden <Matthew.Whilden@microsoft.com> and Scott Wadsworth <bwadswor@microsoft.com>.

We get the apps as a list (csv, json, ...) where the most important part is the download link. This should be an Azure Storage URL with SAS token, so that we can download the content knowing just the URL with no additional Auth required.

## Copying the bundles to our storage account
This is done by taking the URLs provided by the store team and copying the blobs into our storage account. We used this script: `\\fxcore\apps\WindowsStore\UWP\dec17\Import-Apps.ps1`
Note that the script contains a Storage Account Key at the beginning. The value in the script is not valid. You need to get a valid one here:  `https://ms.portal.azure.com/#resource/subscriptions/9c035fa3-535f-4bf9-a60a-1381e6d27ea5/resourceGroups/dotnetAppCompat/providers/Microsoft.Storage/storageAccounts/dotnetappcompat/keys`. Copy the script locally, replace the Storage Account Key and run it.
The script also contains the name of the storage container to copy the blobs to. This should be changed to point a new container to avoid duplicates and other issues.
The input for the script is a CSV file with the apps. Currently the script expects a CSV file with columns `Id`, `Name` and `URL`:
* Id is the identifier used by the store to identify the app. Note that this is not unique, it can contain multiple versions of the same app. For example: `9NBLGGH5WXNW`
* Name is the name of the blob, for example `QyClient_4.1.1.0_x86_x64_arm.appxbundle`.
* URL is the storage URL with the access key in it. For example (cut the access key): `https://ingestionpackagesprod1.blob.core.windows.net/ingestion/ab83f4ff-8196-4a58-9e7b-7407f8287ba8?snapshot=2017-10-26T06:12:31.3873024Z&sv=2016-05-31&sr=b&sig=...&se=2018-02-01T19:36:20Z&sp=rl`.
Note that the script uses all three values. It creates a directory using the Id, then in it a blob using the Name.

The copy process will take a while since we got more than 50K bundles. We sped it up by splitting the input CSV into multiple chunks and running the script in parallel on each chunk separately. The script can handle already uploaded apps (skips those). The full input we used last time is `\\fxcore\apps\WindowsStore\UWP\dec17\Apps-20171201.csv`. That folder also contains the chunks as a sample.

## Preparation
For lot of the processing we have a tool call AppManager. It lives alongside the other AppCompat tools in `https://devdiv.visualstudio.com/defaultcollection/DevDiv/_git/CoreFxAppCompat?path=%2FTools&version=GBmaster&_a=contents`. The version used for the last time is also stored on the share `\\fxcore\apps\WindowsStore\UWP\dec17\AppManager`. It can handle multiple different operations - basically a set of utilities to handle the extraction and other related tasks. Make a local copy of the tool and all its dependencies before running (for simplicity).
In the below samples I will use `C:\ext` as the working directory for the extraction and so on. Please note that the path to this folder is not hardcoded anywhere, it's specified as input to all the tools, so use the right drive (needs a LOT of free space. at least 300GB). Also note that the path to this folder should be as short as possible. Some apps have deep directory structures and during the extraction we could hit "Path too long" issues.
Make sure that you disable anti-virus for the `C:\ext` otherwise things will be VERY slow and error prone.
The tool needs makeappx.exe which is part of the Windows SDK, one is available in `\\fxcore\apps\WindowsStore\UWP\dec17\Win10SDK`. Make a local copy of that folder into C:\ext\Win10SDK.

## Enumerating downloaded blobs
We copy the blobs to our storage just to get a snapshot from the store, so that we can do the rest without any dependency on the store team/servers. In theory we could start directly from the store without a copy, but it would make things time sensitive and possibly more complicated.
Now we create a list of all the blobs we have available. This is done by running:

`AppManager.exe enumeratebundles --outputCSV C:\ext\bundles.csv apps-dec17`

The first parameter is the file to write the list to, the second parameter is the name of the storage container into which we copied the blobs (`apps-dec17` in this case).
The tool will produce a CSV with URLs and names and size information about all the blobs. This is later used to drive the extraction.

## Extracting the apps
We get "bundles" from the store. So, either .appx or .appxbundle files. The .appx one is a single app version, .appxbundle is an archive with multiple versions of the app in it (typically different architectures). So we need to extract these (it's a zip, but simple unzip corrupts some file names), split it to separate apps (in case of the bundles) and then upload to our final storage where we take the apps from for testing. We zip the app before uploading it then (with normal zip, not as .appx).
The extraction process while theoretically simple is tedious because it takes a long time and is prone to random failures. It is done by the AppManager via:

`AppManager.exe extractappsfromstorage --workFolder C:\ext --bundleList C:\ext\bundles.csv --startFrom 0 --appCount 1000 --targetContainer apps-dec17-extracted --makeappx C:\ext\Win10SDK\makeappx.exe`

* `workFolder` points to the local hard drive folder which will be used for everything. Temporary files/folders will be created there, log files are written there as well as result lists. As noted above the path should be as short as possible and the drive needs to have lot of free space.
* `bundleList` points to a CSV with the list of bundles to extract (created in the previous step). Technically it only uses the first column, which must be the URL to the blob to process, the rest of the CSV is ignored. It expects the CSV to have the header line.
* `startFrom`/`appCount` specifies the range of the blobs to process. `startFrom` is the index of the first blob in the CSV to process, `appCount` is the number of blobs after that to process. This is used to split the input into chunks.
* `targetContainer` is the name of the container where to upload the extracted apps to.
* `makeappx` points to the makeappx.exe to use (should be local).

The extraction is slow, very slow. I suggest running it inside an Azure VM so that it can be left alone for a while. Using more than one VM is a good idea to speed things app. The limiting factors for the speed are:
* Azure download - downloading the blobs to process is the slowest part. Running multiple copies in parallel even on the same machine speeds this up a lot (each copy will get only about 20MB/s, regardless of how many are running). Some bundles have multiple GBs, in fact there are several test packages which are on the order of 100GBs. Either wait for it, or just remove them from the list if we're OK to not have them (they are not real apps as far as I know, and we would not use them for testing anyway since they're too big).
* Hard drive - the extraction itself is typically IO bound as it reads/writes lot of data. So, limit the number of concurrent copies to something like 5 on each VM, or get a VM with SSD.
* CPU - the extraction and upload are zip/unzip operations which eat CPU. Using VM with 4 cores is advisable.

Logs are produced into a subdirectory of `C:\ext` with the current date in the name of the folder. Each copy of the tool produces a different log file. On top of that, each extraction will produce a separate log for that app. Use these to figure out failures.

Chunking this is done using the `startFrom`/`appCount` parameters. The tool will produce logs/resultlist with names using the name of the bundlelist and the offsets processed. So multiple copies of the tool can run in the same `C:\ext` if they're not processing the same range.
Even with all this the extraction is bound to fail sometimes due to unexpected reasons. For me it failed a lot as I was still developing the AppManager tool and hardening it against flaky issues. For this purpose the tool writes out `C:\ext\somename.failed.csv` file which contains the list of the bundle URLs which failed for whatever reason. The file can be used as the input `bundleList` for the tool for easy retries. In my experience this fixes almost all failures - by simply running it again.
The tool does NOT skip already extracted apps, this is by design to avoid potential partial uploads and so on. Running it twice on the same input will not break anything, just take a long time.

For the successfully extracted apps the tool writes out `C:\ext\somename.apps.txt` file which contains a list of the extracted apps. The file is a text file where each line represents one extracted application. The line is a JSON object describing the app. Note that one bundle can (and typically will) extract into multiple apps.

## Merging the list of apps
Once all the apps are extracted, collect all the `*.apps.txt` files into one folder (for example `C:\ext\applist`) and run

`AppManager.exe mergeextractedapplists C:\ext\applist\*.apps.txt C:\ext\Extracted.apps.txt`

This will remove duplicates (it doesn't verify that the line is identical, but they should be as the extraction process is deterministic and repeatable) and merge all the files into one. The format is still the same, each line is a JSON object describing the app. No ordering is applied, it's effectively random.

## Exporting the list of apps into SQL
After this, run

`AppManager.exe exportextractedapps C:\ext\Extracted.apps.txt C:\ext\Extracted.apps.csv`

This will read the list of apps, parse it, compute some additional properties (like figure out store IDs from the URLs), flatten the information (dependency packages and build properties) into columns and write everything out as a CSV file.
Review the produced .csv file as it should be humanly readable. Make sure that the number of rows is correct, and that the data look correct.

Now to write the data to SQL a new table should be created. So, run:

`AppManager.exe createtablesqlforextractedapps C:\ext\Extracted.apps.txt C:\ext\CreateAllAppsTable.sql`

This creates a SQL statement file to create a SQL DB Table for the CSV.
Note that the SQL statement is not fully finished, it's missing table name and constrain names for example. Please carefully review it before using.

So far, no modifications to the DB were made.

Pick a name for the table. For the above attempt, I picked `Dec17_AppList` and modify the SQL statement with that name. Now run the SQL statement against the DB. This step is intentionally manual so that the review of the table and data is performed before it's exported to the DB itself.

Now insert all the apps into the DB, so run:

`AppManager.exe insertexportedappsintodb --sqllog C:\ext\sqllog C:\ext\Extracted.apps.txt <tablename>`

The `<tablename>` must be the name of the create table above. The table should be empty since the command doesn't check for duplicates in any way.
Please note that this might take a long time (hours) as I didn't spend much time trying to optimize the SQL call sequence (improve batching and such). The last import which was cca 114K apps took more than 3 hours to insert.

## Applying ranking to the apps
Last time I got ranking by asking for an ordered list of apps by purchase count. I specifically asked to get the apps identified by the BigID (the ID which always starts with 9). The person who helped was Zach Capehart <zacape@microsoft.com>. I got back a file which is in `\\fxcore\apps\WindowsStore\UWP\dec17\ranking\OrderedApps.txt"`. Using that file I then ran:

`AppManager.exe updateapprank <tablename> OrderedApps.txt`

This was VERY slow, so I ended up modifying the table to change the type of the `StoreAppId` column to `VARCHAR(15)` and creating a non-clustered index on it. After that the above command finished in about 2 hours for the cca 31K apps in the OrderedApps file.

It's very possible that next time we will get the ranking data in a different format, so this process will have to be tweaked. But the goal is to fill the Rank column in the DB for the apps we have information about. The most "popular" app should be rank #1, second #2 and so on. Apps which we don't have information about should be left with rank NULL (we treat these as having rank after all those which do have a non-null value).

## Generating app product and version GUIDs
The entire AppCompat infrastructure uses special identifiers to uniquely identify each app. For the infrastructure each version of each architecture of each app is treated as a unique app. Grouping of all versions and architectures of a given app is done only for purposes of easier consumption by humans (so that we can match the app behavior across architectures for example). For this purpose each app is assigned a GUID which is called a product GUID. Each version/architecture of an app is then assigned another GUID which is called a version GUID. The pair of the GUIDs formated as a string with underscore between them, so `"<product GUID>_<version GUID>"` is then used as the unique identifier of the application.
To generate these identifiers, run the following command:

`AppManager.exe generateappguids <tablename>`

This will query the DB for all apps in the specified table which don't have any identifier assigned and will generate them and update the DB. This is not super fast, so expect it to take a couple of hours to finish. If the operation fails or you interrupt it running it again will resume, with one minor issue. If some versions of an app were alreasy assigned identifiers and some didn't for the same up, the second run will assign a new product identifier to this app. Not a big problem as nothing relies on this, but it's better to not interrupt this command.

## Add app size data
It's also possible to add basic size information to the DB. This helps to filter out very large packages which would be unsuitable for testing for example.
Add a new column to the table called ZipSize:

`ALTER TABLE <tablename> ADD ZipSize BIGINT`

And then run this command to fill it:

`AppManager.exe addappsizetodb <tablename>`

This will take a while (2 hours cca) as it has to go over all the apps both in the DB and in the blob storage.