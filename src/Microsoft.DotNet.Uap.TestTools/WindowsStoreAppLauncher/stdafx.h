// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

// Force a minimum compiler version for VSC++ (security requirement)
#if _MSC_VER < 1900 
#error "Minimum compiler version not found. Minimum version = 1900 (Visual Studio 2015)."
#endif

#include <Windows.Applicationmodel.Activation.h>
#include <windows.management.deployment.h>
#include <windows.foundation.h>
#include <Windows.h>
#include <wrl.h>
#include <memory>
#include <AppxPackaging.h>
#include <PathCch.h>
#include <UserEnv.h>
#include <sddl.h>
#include <fstream>
#include <iterator>
#include <stdio.h>
#include <string>
#include <fstream>
#include <iterator>
#include <wrl.h>
#include <sstream>
#include <AppxPackaging.h>
#include <roapi.h>
#include <Sddl.h>
#include <Shlwapi.h>
#include <Shobjidl.h>
#include <Userenv.h>
#include <windows.management.deployment.h>
#include <windows.applicationmodel.activation.h>
#include <wrl.h>
#include <Psapi.h>
#include <PathCch.h>
#include <memory>
#include <string>
#include <exception>
#include <cstring>
#include <fstream>
#include <sstream>
#include <string>
#include <vector>
#include <cstdio>
#include <wrl\wrappers\corewrappers.h>
#define INITGUID
#include <guiddef.h>
