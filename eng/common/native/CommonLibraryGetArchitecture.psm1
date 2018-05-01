function CommonLibraryGetArchitecture {
    $ProcessorArchitecture = $Env:PROCESSOR_ARCHITECTURE
    $ProcessorArchitectureW6432 = $Env:PROCESSOR_ARCHITEW6432
    if($ProcessorArchitecture -Eq "X86")
    {
        if(($ProcessorArchitectureW6432 -Eq "") -Or 
           ($ProcessorArchitectureW6432 -Eq "X86")) {
            return "x86"
        }
        $ProcessorArchitecture = $ProcessorArchitectureW6432
    }
    if (($ProcessorArchitecture -Eq "AMD64") -Or (
         $ProcessorArchitecture -Eq "IA64") -Or (
         $ProcessorArchitecture -Eq "ARM64")) {
        return "x64"
    }
    return "x86"
    
}
export-modulemember -function CommonLibraryGetArchitecture