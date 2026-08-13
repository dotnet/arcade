function Get-ValidatedArchiveEntryPath {
  param(
    [Parameter(Mandatory=$true)][string] $ExtractionRoot,
    [Parameter(Mandatory=$true)][string] $EntryName
  )

  if ($EntryName.StartsWith('/') -or
      $EntryName.StartsWith('\') -or
      $EntryName -match '^[A-Za-z]:') {
    throw "Archive entry '$EntryName' uses an absolute path."
  }

  $DirectorySeparator = [System.IO.Path]::DirectorySeparatorChar
  $NormalizedEntryName = $EntryName.Replace('/', $DirectorySeparator).Replace('\', $DirectorySeparator)
  if ($DirectorySeparator -eq '\' -and $NormalizedEntryName.Contains(':')) {
    throw "Archive entry '$EntryName' uses an NTFS alternate data stream."
  }

  if ([System.IO.Path]::IsPathRooted($NormalizedEntryName)) {
    throw "Archive entry '$EntryName' uses an absolute path."
  }

  $CanonicalRoot = [System.IO.Path]::GetFullPath($ExtractionRoot)
  $RootPrefix = if ($CanonicalRoot.EndsWith([string]$DirectorySeparator)) {
    $CanonicalRoot
  } else {
    $CanonicalRoot + $DirectorySeparator
  }

  $CandidatePath = [System.IO.Path]::GetFullPath(
    [System.IO.Path]::Combine($CanonicalRoot, $NormalizedEntryName))
  $Comparison = if ($DirectorySeparator -eq '\') {
    [System.StringComparison]::OrdinalIgnoreCase
  } else {
    [System.StringComparison]::Ordinal
  }

  if (!$CandidatePath.StartsWith($RootPrefix, $Comparison)) {
    throw "Archive entry '$EntryName' resolves outside extraction root '$CanonicalRoot'."
  }

  return $CandidatePath
}
