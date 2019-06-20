// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "stdafx.h"
#include "WindowsStoreApp.h"
#include "Stream.h"
#include "Support.h"

using namespace ABI::Windows::Management::Deployment;
using namespace ABI::Windows::ApplicationModel::Activation;
using namespace ABI::Windows::ApplicationModel;
using namespace ABI::Windows::Foundation::Collections;
using namespace Microsoft::WRL;
using namespace Microsoft::WRL::Wrappers;
using namespace std;

WindowsStoreApp::WindowsStoreApp(const wstring& manifestPath, const wstring& appId)
:AppxApp(manifestPath, appId)
{
}

WindowsStoreApp::~WindowsStoreApp()
{
}

ComPtr<IApplicationActivationManager> WindowsStoreApp::GetApplicationActivationManager()
{
  ComPtr<IApplicationActivationManager> applicationActivationManager;
  IFT(CoCreateInstance(CLSID_ApplicationActivationManager, nullptr, CLSCTX_LOCAL_SERVER, IID_PPV_ARGS(&applicationActivationManager)));
  return applicationActivationManager;
}

void WindowsStoreApp::Add()
{
  // Create the package Uri
  ComPtr<IUriRuntimeClassFactory> uriRuntimeClassFactory;
  ComPtr<IUriRuntimeClass> packagerUri;

  dwprintf(L"GetActivationFactory...\n");
  IFT(RoGetActivationFactory(HStringReference(RuntimeClass_Windows_Foundation_Uri).Get(), __uuidof(IUriRuntimeClassFactory), &uriRuntimeClassFactory));
  dwprintf(L"CreateUri, path %s...\n", get_ManifestPath().c_str());
  HString manifestPath;
  manifestPath.Set(get_ManifestPath().c_str(), (unsigned int)get_ManifestPath().length());
  IFT(uriRuntimeClassFactory->CreateUri(manifestPath.Get(), &packagerUri));

  // Create the PackageManager
  ComPtr<IInspectable> packageMangerInspectable;
  ComPtr<IPackageManager> packageManger;

  dwprintf(L"RoActivateInstance...\n");
  IFT(RoActivateInstance(HStringReference(RuntimeClass_Windows_Management_Deployment_PackageManager).Get(), &packageMangerInspectable));
  dwprintf(L"QueryInterface...\n");
  IFT(packageMangerInspectable.As(&packageManger));

  // Register the package asynchronously
  ComPtr<IAsyncOperationWithProgress<DeploymentResult*, DeploymentProgress>> registerPackageAsyncOperation;

  dwprintf(L"RegisterPackageAsync...\n");
  IFT(packageManger->RegisterPackageAsync(
    packagerUri.Get(),
    nullptr,
    ABI::Windows::Management::Deployment::DeploymentOptions::DeploymentOptions_DevelopmentMode,
    &registerPackageAsyncOperation));

  // TODO: Get DeploymentResult::ErrorText

  dwprintf(L"Wait...\n");
  // Wait for the package to be registered and return the result
  Wait<DeploymentResult*, DeploymentProgress>(registerPackageAsyncOperation.Get(), nullptr);
}

void WindowsStoreApp::Remove()
{
  // Create the PackageManager
  ComPtr<IInspectable> packageMangerInspectable;
  ComPtr<IPackageManager> packageManger;

  IFT(RoActivateInstance(HStringReference(RuntimeClass_Windows_Management_Deployment_PackageManager).Get(), &packageMangerInspectable));
  IFT(packageMangerInspectable.As(&packageManger));

  ComPtr<IIterable<Package*>> packageCollection;
  IFT(packageManger->FindPackagesByUserSecurityIdPackageFamilyName(nullptr, HStringReference(get_PackageFamilyName().c_str()).Get(), &packageCollection));

  ComPtr<IIterator<Package*>> packageIterator;
  IFT(packageCollection->First(&packageIterator));

  boolean hasCurrent;
  for (packageIterator->get_HasCurrent(&hasCurrent); hasCurrent; packageIterator->MoveNext(&hasCurrent))
  {
      ComPtr<IPackage> package;
      IFT(packageIterator->get_Current(&package));

      ComPtr<IPackageId> packageId;
      IFT(package->get_Id(&packageId));

      HSTRING hstrPackageIdName;
      IFT(packageId->get_FullName(&hstrPackageIdName));
      HString packageIdName;
      packageIdName.Attach(hstrPackageIdName);

      // Remove the package asynchronously
      ComPtr<IAsyncOperationWithProgress<DeploymentResult*, DeploymentProgress>> removePackageAsyncOperation;
      IFT(packageManger->RemovePackageAsync(
          packageIdName.Get(),
          &removePackageAsyncOperation));

      // TODO: Get DeploymentResult::ErrorText

      // Wait for the package to be removed and return the result
      Wait<DeploymentResult*, DeploymentProgress>(removePackageAsyncOperation.Get(), nullptr);
  }
}

wstring WindowsStoreApp::GetAppContainerFolderPath() const
{
  wstring folderPath(GetEnvironmentVariable(L"USERPROFILE"));
  folderPath += L"\\AppData\\Local\\Packages\\";
  folderPath += get_PackageFamilyName();
  folderPath += L"\\";
  wprintf_s(L"Resolved Folder Path: %s\n", folderPath.c_str());

  return folderPath;
}
