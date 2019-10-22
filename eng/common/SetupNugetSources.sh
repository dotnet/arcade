#!/usr/bin/env bash

ConfigFile=$1
CredToken=$2
NL='\n'
TB='\t'

if [[ `uname -s` == "Darwin" ]]; then
	NL=$'\\\n'
	TB=''
fi

echo $OSTYPE
uname -s

# Ensure there is a <packageSources>...</packageSources> section.
grep -i "<packageSources>" $ConfigFile 
if [ "$?" != "0" ]; then
	echo "Adding <packageSources>...</packageSources> section."
	ConfigNodeHeader="<configuration>"
	PackageSourcesTemplate="${TB}<packageSources>${NL}${TB}</packageSources>"

	sed -i.bak "s|$ConfigNodeHeader|$ConfigNodeHeader${NL}$PackageSourcesTemplate|" NuGet.config
fi

# Ensure there is a <packageSourceCredentials>...</packageSourceCredentials> section. 
grep -i "<packageSourceCredentials>" $ConfigFile 
if [ "$?" != "0" ]; then
	echo "Adding <packageSourceCredentials>...</packageSourceCredentials> section."

	PackageSourcesNodeFooter="</packageSources>"
	PackageSourceCredentialsTemplate="${TB}<packageSourceCredentials>${NL}${TB}</packageSourceCredentials>"

	sed -i.bak "s|$PackageSourcesNodeFooter|$PackageSourcesNodeFooter${NL}$PackageSourceCredentialsTemplate|" NuGet.config
fi

# Ensure dotnet3-internal and dotnet3-internal-transport is in the packageSources
grep -i "<add key=\"dotnet3-internal\">" $ConfigFile 
if [ "$?" != "0" ]; then
	echo "Adding dotnet3-internal to the packageSources."

	PackageSourcesNodeFooter="</packageSources>"
	PackageSourceTemplate="${TB}<add key=\"dotnet3-internal\" value=\"https://pkgs.dev.azure.com/dnceng/_packaging/dotnet3-internal/nuget/v2\" />"

	sed -i.bak "s|$PackageSourcesNodeFooter|$PackageSourceTemplate${NL}$PackageSourcesNodeFooter|" NuGet.config
fi

# Ensure dotnet3-internal and dotnet3-internal-transport is in the packageSources
grep -i "<add key=\"dotnet3-internal-transport\">" $ConfigFile 
if [ "$?" != "0" ]; then
	echo "Adding dotnet3-internal-transport to the packageSources."

	PackageSourcesNodeFooter="</packageSources>"
	PackageSourceTemplate="${TB}<add key=\"dotnet3-internal-transport\" value=\"https://pkgs.dev.azure.com/dnceng/_packaging/dotnet3-internal-transport/nuget/v2\" />"

	sed -i.bak "s|$PackageSourcesNodeFooter|$PackageSourceTemplate${NL}$PackageSourcesNodeFooter|" NuGet.config
fi

# I want things split line by line
PrevIFS=$IFS
IFS=$'\n'
PackageSources=$(grep -oh '"darc-int-[^"]*"' $ConfigFile | tr -d '"')
IFS=$PrevIFS

PackageSources+=('dotnet3-internal')
PackageSources+=('dotnet3-internal-transport')

for FeedName in ${PackageSources[@]} ; do
	# Check if there is no existing credential for this FeedName
	grep -i "<$FeedName>" $ConfigFile 
	if [ "$?" != "0" ]; then
		echo "Adding credentials for $FeedName."

		PackageSourceCredentialsNodeFooter="</packageSourceCredentials>"
		NewCredential="${TB}${TB}<$FeedName>${NL}<add key=\"Username\" value=\"dn-bot\" />${NL}<add key=\"ClearTextPassword\" value=\"$CredToken\" />${NL}</$FeedName>"

		sed -i.bak "s|$PackageSourceCredentialsNodeFooter|$NewCredential${NL}$PackageSourceCredentialsNodeFooter|" NuGet.config
	fi
done

echo 
echo
cat $ConfigFile
