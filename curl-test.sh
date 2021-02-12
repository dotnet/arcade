#!/bin/bash

install_script_url="https://dot.net/v1/dotnet-install.sh"
curl "$install_script_url" -sSL --verbose --retry 10 --create-dirs -o "dotnet-install.sh"
