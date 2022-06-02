# Pipeline Status

## Estimated Times

| Item                 | Estimated Time |
| -------------------- | -------------- |
| Obtain build machine | **13m 4s**     |
| Pipeline to complete | **1hr 18m**    |

Here's a list of the top 5 most congested queues in your pipeline:

| Queue                              | Work Item Wait Time | Difference in Moving Avg |
| ---------------------------------- | ------------------- | ------------------------ |
| [`Windows.11.Amd64.Client.Open`]() | **43min 2s**        | *+12%* 📈                 |
| [`Ubuntu.1804.Amd64.Open`]()       | **43min 2s**        | *-3.0%* 📉                |
| [`Debian.11.Amd64.Open`]()         | **43min 2s**        | *+0.1%* 📈                |
| [`Windows.11.Amd64.Client.Open`]() | **43min 2s**        | *+1%* 📈                  |
| [`Windows.11.Amd64.Client.Open`]() | **43min 2s**        | *-7%* 📉                  |


## Queue Insights

❌ The queue [`OSX.1015.Amd64.Open`]() is overloaded.
* **Your tests will likely timeout**.
* Current queue count: **560** (*+57%* over moving average)

⚠️ Currently, [`Windows.10.Amd64.Client21H1.Open`]() is experiencing a high volume of traffic.
* Estimated time in queue: **35m**. (*+22%*)
* There are no known issues with our infrastructure.
* ❗**There is currently a known issue with our infrastructure.** [Details.]()

✅ [`OSX.1200.ARM64.Open`]() has unusually low traffic.
* Estimated time in queue: **3m 4s**. (*-34%*)

## .NET Engineering Services Infrastructure Status

| Product        | Status |
| -------------- | :----: |
| Helix          |   ✅    |
| Queues         |   ⚠️    |
| On-Prem Queues |   ❌    |

See our [Helix status overview dashboard]().

## Grafana Dashboard

For more in-depth information on the status of Helix, visit our [Grafana Dashboard]().

![](https://raw.githubusercontent.com/dotnet/brand/main/dotnet-bot-illustrations/Website%20Illustrations/apache-spark-analytics-engine-bot-machine.svg)

## Your Queues

☁️ **dotnet/runtime** is currently configured to submit to the following Helix queues:

* `Alpine.313.Amd64.Open`               
* `Alpine.313.Arm64.Open`               
* `Alpine.314.Amd64.Open`            
* `Alpine.314.Arm64.Open`               
* `Centos.7.Amd64.Open`                 
* `Centos.8.Amd64.Open`              
* `Debian.10.Amd64.Open`                
* `Debian.10.Arm32.Open`                
* `Debian.11.Amd64.Open`             
* `Debian.11.Arm32.Open`                
* `Fedora.34.Amd64.Open`                
* `Mariner.1.0.Amd64.Open`           
* `OSX.1015.Amd64.AppleTV.Open`         
* `OSX.1015.Amd64.Iphone.Open`          
* `OSX.1015.Amd64.Open`              
* `OSX.1100.Arm64.Open`                 
* `OSX.1200.ARM64.Open`                 
* `OSX.1200.Amd64.Open`              
* `Raspbian.10.Armv6.Open`              
* `RedHat.7.Amd64.Open`                 
* `SLES.15.Amd64.Open`               
* `Ubuntu.1804.Amd64`                   
* `Ubuntu.1804.Amd64.Android.29.Open`   
* `Ubuntu.1804.Amd64.Open`           
* `Ubuntu.1804.ArmArch.Open`            
* `Ubuntu.2004.S390X.Experimental.Open` 
* `Ubuntu.2110.Amd64.Open`           
* `Ubuntu.2110.Arm64.Open`              
* `Windows.10.Amd64.Android.Open`       
* `Windows.10.Amd64.Client21H1.Open` 
* `Windows.10.Amd64.Server2022.ES.Open` 
* `Windows.10.Amd64.ServerRS5.Open`     
* `Windows.10.Arm64.Open`            
* `Windows.10.Arm64v8.Open`             
* `Windows.11.Amd64.Client.Open`        
* `Windows.7.Amd64.Open`             
* `Windows.81.Amd64.Open`               
* `Windows.Amd64.Server2022.Open`       
* `Windows.Nano.1809.Amd64.Open`     
* `openSUSE.15.2.Amd64.Open`           

🏢 **dotnet/runtime** uses the following on-prem queues:

* `Some.OnPrem.Queue`
* `Some.OnPrem.Queue2`
* `Some.OnPrem.Queue3`

*Was this helpful?* 👍👎