// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using MSBuild = Microsoft.Build.Utilities;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.DotNet.Build.Tasks.Feed
{
    public abstract class MSBuildTaskBase : MSBuild.Task
    {
        protected readonly IFileSystem _fileSystem;

        public MSBuildTaskBase(ServiceProvider provider = null)
        {
            if(provider == null)
            {
                provider = ConfigureServices();
            }

            _fileSystem = provider.GetRequiredService(typeof(IFileSystem)) as IFileSystem;
        }

        private ServiceProvider ConfigureServices()
        {
            ServiceProvider provider = new ServiceCollection()
                .AddSingleton<ISigningInformationModelFactory, SigningInformationModelFactory>()
                .AddSingleton<IBlobArtifactModelFactory, BlobArtifactModelFactory>()
                .AddSingleton<IPackageArtifactModelFactory, PackageArtifactModelFactory>()
                .AddSingleton<IBuildModelFactory, BuildModelFactory>()
                .AddSingleton<IFileSystem, FileSystem>()
                .BuildServiceProvider();

            return provider;
        }

        public const string AssetsVirtualDir = "assets/";

        public static ISigningInformationModelFactory SigningInformationModelFactory { get; set; } = new SigningInformationModelFactory();

        public static IBlobArtifactModelFactory BlobArtifactModelFactory { get; set; } = new BlobArtifactModelFactory();

        public static IPackageArtifactModelFactory PackageArtifactModelFactory { get; set; } = new PackageArtifactModelFactory();

        public static IBuildModelFactory BuildModelFactory { get; set; } = new BuildModelFactory(SigningInformationModelFactory, BlobArtifactModelFactory, PackageArtifactModelFactory);
    }
}
