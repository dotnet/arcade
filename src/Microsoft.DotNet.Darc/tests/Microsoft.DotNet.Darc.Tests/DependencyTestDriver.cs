using Microsoft.DotNet.DarcLib;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.DotNet.Darc.Tests
{
    /// <summary>
    ///     A driver that set and cleans up a dependency test.
    ///     Specifically, this class:
    ///     - Takes a test input folder (effectively a fake git repo)
    ///       and copies it to a temp location where it can be modified.
    ///     - Enables comparison of expected outputs.
    ///     - Cleans up after test
    /// </summary>
    internal class DependencyTestDriver
    {
        private string _testName;
        private LocalGitClient _gitClient;
        private GitFileManager _gitFileManager;
        private const string inputRootDir = "inputs";
        private const string inputDir = "input";
        private const string outputDir = "output";

        public string TemporaryRepositoryPath { get; private set; }
        public string RootInputsPath { get => Path.Combine(Environment.CurrentDirectory, inputRootDir, _testName, inputDir); }
        public string RootExpectedOutputsPath { get => Path.Combine(Environment.CurrentDirectory, inputRootDir, _testName, outputDir); }
        public LocalGitClient GitClient { get => _gitClient; }
        public GitFileManager GitFileManager { get => _gitFileManager; }

        public DependencyTestDriver(string testName)
        {
            _testName = testName;
        }

        /// <summary>
        ///     Set up the test, copying inputs to the temp repo
        ///     and creating a git file manager for that repo
        /// </summary>
        public void Setup()
        {
            // Create the temp repo dir
            TemporaryRepositoryPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(TemporaryRepositoryPath);

            // Copy all inputs to that temp repo
            CopyDirectory(RootInputsPath, TemporaryRepositoryPath);

            // Set up a git file manager
            _gitClient = new LocalGitClient(NullLogger.Instance);
            _gitFileManager = new GitFileManager(GitClient, NullLogger.Instance);
        }

        public async Task AddDependencyAsync(DependencyDetail dependency, DependencyType dependencyType)
        {
            await _gitFileManager.AddDependencyAsync(
                dependency,
                dependencyType,
                TemporaryRepositoryPath,
                null);
        }

        public async Task UpdateDependenciesAsync(List<DependencyDetail> dependencies)
        {
            GitFileContentContainer container = await _gitFileManager.UpdateDependencyFiles(
                dependencies,
                TemporaryRepositoryPath,
                null);
            List<GitFile> filesToUpdate = container.GetFilesToCommit();
            await _gitClient.PushFilesAsync(filesToUpdate, TemporaryRepositoryPath, null, null);
        }

        public async Task VerifyAsync()
        {
            await _gitFileManager.Verify(TemporaryRepositoryPath, null);
        }

        public async Task<DependencyGraph> GetDependencyGraph(DependencyDetail dependency)
        {
            return await DependencyGraph.GetDependencyGraphAsync(
                null, 
                dependency, 
                false, 
                NullLogger.Instance, 
                testPath: TemporaryRepositoryPath);
        }

        private async static void TestAndCompareImpl(
            string testInputsName, 
            bool compareOutput, 
            Func<DependencyTestDriver, Task> testFunc)
        {
            DependencyTestDriver dependencyTestDriver = new DependencyTestDriver(testInputsName);
            try
            {
                dependencyTestDriver.Setup();
                await testFunc(dependencyTestDriver);
                if (compareOutput)
                {
                    await dependencyTestDriver.AssertEqual(VersionFiles.VersionDetailsXml, VersionFiles.VersionDetailsXml);
                    await dependencyTestDriver.AssertEqual(VersionFiles.VersionProps, VersionFiles.VersionProps);
                    await dependencyTestDriver.AssertEqual(VersionFiles.GlobalJson, VersionFiles.GlobalJson);
                }
            }
            finally
            {
                dependencyTestDriver.Cleanup();
            }
        }

        public static void TestAndCompareOutput(string testInputsName, Func<DependencyTestDriver, Task> testFunc)
        {
            TestAndCompareImpl(testInputsName, true, testFunc);
        }

        public static void TestNoCompare(string testInputsName, Func<DependencyTestDriver, Task> testFunc)
        {
            TestAndCompareImpl(testInputsName, false, testFunc);
        }

        public async static void GetGraphAndCompare(string testInputsName, 
            Func<DependencyTestDriver, Task<DependencyGraph>> testFunc,
            Func<DependencyDetail, string, string, Task<DependencyGraph>> getExpectedDependencyGraph,
            DependencyDetail rootDependency,
            string outputFileName,
            bool equal)
        {
            DependencyTestDriver dependencyTestDriver = new DependencyTestDriver(testInputsName);

            try
            {
                dependencyTestDriver.Setup();
                DependencyGraph dependencyGraph = await testFunc(dependencyTestDriver);
                DependencyGraph expectedDependencyGraph = await getExpectedDependencyGraph(
                    rootDependency, 
                    dependencyTestDriver.TemporaryRepositoryPath, 
                    outputFileName);

                if (equal)
                {
                    dependencyTestDriver.AssertEqual(dependencyGraph, expectedDependencyGraph);
                }
                else
                {
                    dependencyTestDriver.AssertNotEqual(dependencyGraph, expectedDependencyGraph);
                }
            }
            finally
            {
                dependencyTestDriver.Cleanup();
            }
        }

        /// <summary>
        ///     Determine whether a file in the input path is the same a file in the output path.
        /// </summary>
        /// <param name="actualOutputPath">Subpath to the outputs in the temporary repo</param>
        /// <param name="expectedOutputPath">Subpath to the expected outputs</param>
        public async Task AssertEqual(string actualOutputPath, string expectedOutputPath)
        {
            string expectedOutputFilePath = Path.Combine(RootExpectedOutputsPath, expectedOutputPath);
            string actualOutputFilePath = Path.Combine(TemporaryRepositoryPath, actualOutputPath);
            using (StreamReader expectedOutputsReader = new StreamReader(expectedOutputFilePath))
            using (StreamReader actualOutputsReader = new StreamReader(actualOutputFilePath))
            {
                string expectedOutput = await expectedOutputsReader.ReadToEndAsync();
                string actualOutput = await actualOutputsReader.ReadToEndAsync();
                Assert.Equal(
                    expectedOutput,
                    actualOutput);
            }
        }

        /// <summary>
        ///     Determine whether two DependencyGraphs are the same.
        /// </summary>
        /// <param name="actualDependencyGraph">The generated graph</param>
        /// <param name="expectedDependencyGraph">The expected graph</param>
        public void AssertEqual(DependencyGraph actualDependencyGraph, DependencyGraph expectedDependencyGraph)
        {
            Assert.Equal(actualDependencyGraph.Graph.DependencyDetail.Commit, expectedDependencyGraph.Graph.DependencyDetail.Commit);
            Assert.Equal(actualDependencyGraph.Graph.DependencyDetail.Name, expectedDependencyGraph.Graph.DependencyDetail.Name);
            Assert.Equal(actualDependencyGraph.Graph.DependencyDetail.RepoUri, expectedDependencyGraph.Graph.DependencyDetail.RepoUri);
            Assert.Equal(actualDependencyGraph.Graph.DependencyDetail.Version, expectedDependencyGraph.Graph.DependencyDetail.Version);
            Assert.True(actualDependencyGraph.FlatGraph.SetEquals(expectedDependencyGraph.FlatGraph));
            Assert.True(actualDependencyGraph.Graph.ChildNodes.SetEquals(expectedDependencyGraph.Graph.ChildNodes));
        }

        /// <summary>
        ///     Determine whether two DependencyGraphs are different.
        /// </summary>
        /// <param name="actualDependencyGraph">The generated graph</param>
        /// <param name="expectedDependencyGraph">The expected graph</param>
        public void AssertNotEqual(DependencyGraph actualDependencyGraph, DependencyGraph expectedDependencyGraph)
        {
            Assert.False(actualDependencyGraph.FlatGraph.SetEquals(expectedDependencyGraph.FlatGraph));
            Assert.False(actualDependencyGraph.Graph.ChildNodes.SetEquals(expectedDependencyGraph.Graph.ChildNodes));
        }

        /// <summary>
        ///     Clean temporary files
        /// </summary>
        public void Cleanup()
        {
            Directory.Delete(TemporaryRepositoryPath, true);
        }

        /// <summary>
        ///     Copy a directory, subdirectories and files from <paramref name="source"/> to <paramref name="destination"/>
        /// </summary>
        /// <param name="source">Source directory to copy</param>
        /// <param name="destination">Destination directory to copy</param>
        private void CopyDirectory(string source, string destination)
        {
            if (!Directory.Exists(destination))
            {
                Directory.CreateDirectory(destination);
            }

            DirectoryInfo sourceDir = new DirectoryInfo(source);

            FileInfo[] files = sourceDir.GetFiles();
            foreach (FileInfo file in files)
            {
                file.CopyTo(Path.Combine(destination, file.Name), true);
            }

            DirectoryInfo[] subDirs = sourceDir.GetDirectories();
            foreach (DirectoryInfo dir in subDirs)
            {
                CopyDirectory(dir.FullName, Path.Combine(destination, dir.Name));
            }
        }
    }
}
