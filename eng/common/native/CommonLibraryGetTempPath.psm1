function CommonLibraryGetTempPath {
    return Join-Path $Env:USERPROFILE ".net/native/installers/temp"
}
export-modulemember -function CommonLibraryGetTempPath