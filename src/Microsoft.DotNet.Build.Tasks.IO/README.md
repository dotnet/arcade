Microsoft.DotNet.Build.Tasks.IO
===============================

Contains tasks related to file IO.

See ["Task Packages"](../../Documentation/TaskPackages.md#usage) for guidance on installing this package.

Tasks in this package

 - Chmod
 - GetFileHash
 - VerifyFileHash

## Tasks

This package contains the following MSBuild tasks.

### `GetFileHash`

Computes the checksums for files.

Task parameter     | Type         | Description
-------------------|--------------|-------------
Files              | ITaskItem[]  | **[Required]** The files to be hashed.
Algorithm          | string       | The algorithm. Allowed values: SHA256, SHA384, SHA512. Default = SHA256
FileHash           | string       | **[Output]** The hash of the file in hex. This is only set if there was one item group passed in.
FileHashBase64     | string       | **[Output]** The hash of the file base64 encoded. This is only set if there was one item group passed in.
Items              | ITaskItem[]  | **[Output]** The input files with additional metadata set to include the file hash. <br> Metadata items include: FileHashAlgoritm, FileHash, and FileHashBase64.
MetadataName       | string       | The metadata name where the hash is store in each item. File hash is in hex. Defaults to "FileHash"
MetadataNameBase64 | string       | The metadata name where the base64 encoded hash is store in each item. Defaults to "FileHashBase64"

Example:
```xml
<GetFileHash Files="file.txt">
    <Output TaskParameter="FileHash" PropertyName="MyFileChecksum">
</GetFileHash>

<ItemGroup>
   <FilesToHash Include="*.txt" />
</ItemGroup>

<GetFileHash Files="@(FilesToHash)">
    <Output TaskParameter="Items" ItemName="FilesWithHash">
</GetFileHash>

<Message Text="%(FilesWithHash.FullPath) = %(FilesWithHash.FileHash), algorithm = %(FilesWithHash.FileHashAlgoritm) " />
```

### `VerifyFileHash`

Verifies that a checksum for a file

Task parameter     | Type         | Description
-------------------|--------------|-------------
File               | string       | **[Required]** The file to be verified.
Algorithm          | string       | The algorithm. Allowed values: SHA256, SHA384, SHA512. Default = SHA256
FileHash           | string       | **[Required]** The expected hash of the file in hex.

Example:
```xml
<VerifyFileHash File="file.txt" FileHash="B85656CE3BC7045D403CC55E688C872083A89986CBBF5B336684B669AE19CB7E" />
```

### `Chmod`

Changes Unix permissions on files using `chmod`. On Windows, this task is a no-op.

Task parameter    | Type    | Description
------------------|---------|-------------
File              | string  | **[Required]** The file to be chmod-ed.
Mode              | string  | **[Required]** The file mode to be used. Mode can be any input supported my `man chmod`. e.g. `+x`, `0755`.
