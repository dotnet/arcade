using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;

/*
 Prototype class. We'll need to: 
    *  Replace KeyVaultManager with the logic we currently use in Helix to get stuff off of KV
    *  Instead of using Console to write the output use an ILogger interface
    *  Probably instead of having queries here, add APIs to Helix or some other place which uses a token level auth
     */
namespace Microsoft.DotNet.Darc
{
    public class Remote
    {
        public static async Task<IEnumerable<DependencyItem>> GetDependantAssetsAsync(string assetName, string version = null, string repoUri = null, string branch = null, string sha = null, DependencyType type = DependencyType.Unknown)
        {
            return await GetAssetsAsync(assetName, RelationType.Dependant, "Getting assets which depend on", version, repoUri, branch, sha, type);
        }

        public static async Task<IEnumerable<DependencyItem>> GetDependencyAssetsAsync(string assetName, string version = null, string repoUri = null, string branch = null, string sha = null, DependencyType type = DependencyType.Unknown)
        {
            return await GetAssetsAsync(assetName, RelationType.Dependency, "Getting dependencies of", version, repoUri, branch, sha, type);
        }

        public static async Task<DependencyItem> GetLatestDependencyAsync(string assetName)
        {
            List<DependencyItem> dependencies = new List<DependencyItem>();

            Console.WriteLine($"Getting latest dependency version for '{assetName}' in the reporting store...");

            assetName = assetName.Replace('*', '%').Replace('?', '%');

            using (SqlConnection connection = new SqlConnection(await KeyVaultManager.GetSecretAsync("LogAnalysisWriteConnectionString")))
            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandText = $@"
SELECT TOP 1 [AssetName]
    ,[Version]
    ,[RepoUri]
    ,[Branch]
    ,[Sha]
    ,[Type] 
FROM AssetDependency
WHERE AssetName like '{assetName}'
ORDER BY DateProduced DESC";
                await connection.OpenAsync();

                SqlDataReader reader = await command.ExecuteReaderAsync();

                dependencies = await BuildDependencyItemCollectionAsync(reader);

                if (!dependencies.Any())
                {
                    Console.WriteLine($"No dependencies were found matching {assetName}.");
                }

                return dependencies.FirstOrDefault();
            }
        }

        private static async Task<IEnumerable<DependencyItem>> GetAssetsAsync(string assetName, RelationType relationType, string logMessage, string version = null, string repoUri = null, string branch = null, string sha = null, DependencyType type = DependencyType.Unknown)
        {
            string conditionPrefix = relationType == RelationType.Dependant ? "Dependency" : null;
            string selectPrefix = conditionPrefix == null ? "Dependency" : null;
            List<DependencyItem> dependencies = new List<DependencyItem>();

            QueryParameter queryParameters = CreateQueryParameters(assetName, version, repoUri, branch, sha, type, conditionPrefix);

            Console.WriteLine($"{logMessage} {queryParameters.loggingConditions}...");

            using (SqlConnection connection = new SqlConnection(await KeyVaultManager.GetSecretAsync("LogAnalysisWriteConnectionString")))
            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandText = $@"
SELECT DISTINCT [{selectPrefix}AssetName]
    ,[{selectPrefix}Version]
    ,[{selectPrefix}RepoUri]
    ,[{selectPrefix}Branch]
    ,[{selectPrefix}Sha]
    ,[{selectPrefix}Type] 
FROM AssetDependency
WHERE {queryParameters.whereConditions}";
                await connection.OpenAsync();

                SqlDataReader reader = await command.ExecuteReaderAsync();
                dependencies = await BuildDependencyItemCollectionAsync(reader);

                if (!dependencies.Any())
                {
                    Console.WriteLine($"No dependencies were found matching {queryParameters.loggingConditions}.");
                }
            }

            return dependencies;
        }

        private static async Task<List<DependencyItem>> BuildDependencyItemCollectionAsync(SqlDataReader reader)
        {
            List<DependencyItem> dependencies = new List<DependencyItem>();

            if (reader.HasRows)
            {
                while (await reader.ReadAsync())
                {
                    DependencyType dependencyType = (DependencyType)Enum.Parse(typeof(DependencyType), reader.GetString(5));
                    if (!Enum.IsDefined(typeof(DependencyType), dependencyType))
                    {
                        Console.WriteLine($"DependencyType {dependencyType} in not defined in the DependencyType enum. Defaulting to {DependencyType.Unknown}");
                        dependencyType = DependencyType.Unknown;
                    }

                    DependencyItem dependencyItem = new DependencyItem
                    {
                        Name = reader.GetString(0),
                        Version = reader.GetString(1),
                        RepoUri = reader.GetString(2),
                        Branch = reader.GetString(3),
                        Sha = reader.GetString(4),
                        Type = dependencyType,
                    };

                    dependencies.Add(dependencyItem);
                }
            }

            return dependencies;
        }

        private static QueryParameter CreateQueryParameters(string assetName, string version, string repoUri, string branch, string sha, DependencyType type, string prefix = null)
        {
            QueryParameter queryParameters = new QueryParameter();
            queryParameters.loggingConditions.Append($"{prefix}AssetName = '{assetName}'");
            assetName = assetName.Replace('*', '%').Replace('?', '%');
            queryParameters.whereConditions.Append($"{prefix}AssetName like '{assetName}'");

            if (version != null)
            {
                queryParameters.loggingConditions.Append($", {prefix}Version = '{version}'");
                version = version.Replace('*', '%').Replace('?', '%');
                queryParameters.whereConditions.Append($"AND {prefix}Version like '{version}'");
            }

            if (repoUri != null)
            {
                queryParameters.loggingConditions.Append($", {prefix}RepoUri = '{repoUri}'");
                repoUri = repoUri.Replace('*', '%').Replace('?', '%');
                queryParameters.whereConditions.Append($"AND {prefix}RepoUri like '{repoUri}'");
            }

            if (branch != null)
            {
                queryParameters.loggingConditions.Append($", {prefix}Branch = '{branch}'");
                branch = branch.Replace('*', '%').Replace('?', '%');
                queryParameters.whereConditions.Append($"AND {prefix}Branch like '{branch}'");
            }

            if (sha != null)
            {
                queryParameters.loggingConditions.Append($", {prefix}Sha = '{sha}'");
                queryParameters.whereConditions.Append($"AND {prefix}Sha = '{sha}'");
            }

            if (type != DependencyType.Unknown)
            {
                queryParameters.loggingConditions.Append($", {prefix}Type = '{type}'");
                queryParameters.whereConditions.Append($"AND {prefix}Type = '{type}'");
            }

            return queryParameters;
        }
    }
}
