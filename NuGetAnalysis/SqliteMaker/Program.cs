using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NuGet.Frameworks;
using NuGet.Packaging;

namespace SqliteMaker
{
    internal class Program
    {
        public static void Main(string[] args)
        {
            var path = "test.db";
            var baseDirectory = @"D:\nugetAnalysis";
            using (var context = new ApiUsageContext(Path.GetFullPath(path)))
            {
                //var loggerFactory =
                //    (ILoggerFactory) context.GetInfrastructure().GetService(typeof(ILoggerFactory));
                //loggerFactory.AddConsole();

                context.Database.EnsureCreated();

                foreach (var packageFolder in Directory.EnumerateDirectories(baseDirectory).Take(10))
                {
                    var package = Path.GetFileName(packageFolder);
                    var (packageId, packageVersion) = GetIdAndVersion(package);
                    var metadata =
                        JsonConvert.DeserializeObject<PackageMetadata>(
                            File.ReadAllText(Path.Combine(packageFolder, "meta.json")));
                    var packageModel = new Package
                    {
                        Dependencies = JsonConvert.SerializeObject(metadata.Dependencies),
                        Authors = metadata.Authors,
                        DownloadCount = metadata.DownloadCount,
                        LastUpdated = metadata.LastUpdated,
                        Name = packageId,
                        Version = packageVersion,
                        Assemblies = new List<Assembly>(),
                    };

                    foreach (var assembly in Directory.EnumerateFiles(Path.Combine(packageFolder, "content", "lib"), "*.dll", SearchOption.AllDirectories))
                    {
                        var fwFolder = Path.GetFileName(Path.GetDirectoryName(assembly));
                        var fw = NuGetFramework.ParseFolder(fwFolder);
                        var assemblyModel = new Assembly
                        {
                            Name = Path.GetFileName(assembly),
                            TFMId = fw.Framework,
                            TFMVersion = fw.Version.ToString(),
                        };
                        packageModel.Assemblies.Add(assemblyModel);

                        var dataFileForAssembly = Path.Combine(packageFolder, "raw", "lib", fwFolder,
                            Path.GetFileNameWithoutExtension(assembly) + ".json");
                        var dataForAssembly =
                            JsonConvert.DeserializeObject<ApiPortAnalysis>(File.ReadAllText(dataFileForAssembly));
                        foreach (var missingApi in dataForAssembly.MissingDependencies)
                        {
                            var apiKey = Api.GetKey(missingApi.MemberDocId);
                            var api = context.Find<Api>(apiKey);
                            if (api == null)
                            {
                                api = CreateApi(missingApi, apiKey);
                                context.Add(api);
                            }
                            var apiAssembly = context.Find<ApiAssembly>(api.Hash, assemblyModel.AssemblyId);
                            if (apiAssembly == null)
                            {
                                apiAssembly = new ApiAssembly
                                {
                                    Api = api,
                                    Assembly = assemblyModel,
                                };
                                context.Add(apiAssembly);
                            }
                        }
                    }

                    context.Add(packageModel);
                    context.SaveChanges();
                }
            }
        }

        private static Api CreateApi(ApiPortAnalysis.Dependency missingApi, Guid apiKey)
        {
            var docId = missingApi.MemberDocId;
            var kind = docId[0];
            var fullName = docId.Substring(2);
            string name;
            string ns;
            string type;
            switch (kind)
            {
                case 'T':
                {
                    var lastDot = fullName.LastIndexOf('.');
                    name = string.Empty;
                    ns = fullName.Substring(0, lastDot);
                    type = fullName.Substring(lastDot + 1);
                    break;
                }
                case 'F':
                {
                    var lastDot = fullName.LastIndexOf('.');
                    var secondToLastDot = fullName.LastIndexOf('.', lastDot - 1);
                    name = fullName.Substring(lastDot + 1);
                    type = fullName.Substring(secondToLastDot + 1, lastDot - secondToLastDot - 1);
                    ns = fullName.Substring(0, secondToLastDot);
                    break;
                }
                default:
                {
                    var firstOpenParen = fullName.IndexOf('(');
                    var searchStart = firstOpenParen == -1 ? fullName.Length - 1 : firstOpenParen;
                    var lastDot = fullName.LastIndexOf('.', searchStart);
                    var secondToLastDot = fullName.LastIndexOf('.', lastDot - 1);
                    name = fullName.Substring(lastDot + 1);
                    type = fullName.Substring(secondToLastDot + 1, lastDot - secondToLastDot - 1);
                    ns = fullName.Substring(0, secondToLastDot);
                    break;
                }
            }
            return new Api
            {
                Hash = apiKey,
                IsFiltered = false,
                Namespace = ns,
                Type = type,
                Name = name,
                Kind = kind,
                DocId = docId,
            };
        }

        private static (string id, string version) GetIdAndVersion(string package)
        {
            var _ = package.LastIndexOf('_');
            if (_ == -1)
            {
                return (package, string.Empty);
            }
            return (package.Substring(0, _), package.Substring(_ + 1));
        }
    }
}