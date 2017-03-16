

### Misc references:
- Here’s the project you need to add a reference to in order to get a stream to an azure blob: https://mseng.visualstudio.com/Tools/Engineering%20Infrastructure/_git/CoreFX%20Engineering%20Infrastructure?path=%2Fsrc%2Fcommon&version=GBmaster&_a=contents
- Here is the class that has the helpers around blobs (https://mseng.visualstudio.com/Tools/Engineering%20Infrastructure/_git/CoreFX%20Engineering%20Infrastructure?path=%2Fsrc%2Fcommon%2Fcommon%2Fazure%2FAzureBlobStorage.cs&version=GBmaster&_a=contents); right now there’s a constructor that requires a storage account even though the function you need (GetExternalBlobReadStreamFromSasTokenUrlAsync) doesn’t utilize that account. We can add a parameter-less constructor to that class if you need 

- regex for buckets from Jenkins: https://ci.dot.net/failure-cause-management/
- Plug in itself: https://github.com/jenkinsci/build-failure-analyzer-plugin
- XML hurl w/ regex: https://ci.dot.net/userContent/build-failure-analyzer.xml 
