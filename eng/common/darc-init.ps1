$ParentFolder = (get-item $MyInvocation.MyCommand.Path).Directory.FullName

. $ParentFolder\init-tools.ps1

InstallDarcCli