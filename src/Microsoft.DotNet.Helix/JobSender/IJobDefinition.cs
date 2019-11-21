using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Helix.Client
{
    /// <summary>
    /// Job definition with almost all required information already specified
    /// that can be either completed by calling `SendAsync` or further extended.
    /// 
    /// Note that when not running inside Azure DevOps with its set of environment
    /// variables that JobSender captures, Source need to be provided before sending the job.
    /// Also, for non-authenticated scenarios it's required to provide Creator.
    /// </summary>
    public interface IJobDefinition
    {
        /// <summary>
        /// Adds a single work item into the job.
        /// </summary>
        /// <param name="workItemName">Name of the work item.</param>
        /// <returns>Work item fluent builder.</returns>
        IWorkItemDefinitionWithCommand DefineWorkItem(string workItemName);

        /// <summary>
        /// Adds several correlation payloads from URIs.
        /// </summary>
        /// <returns>Fluent job builder.</returns>
        IJobDefinition WithCorrelationPayloadUris(params Uri[] payloadUris);

        /// <summary>
        /// Adds several correlation payloads and corresponding destinations
        /// from URIs. Destination is the place where the payload is extracted
        /// on the target Helix system.
        /// </summary>
        /// <returns>Fluent job builder.</returns>
        IJobDefinition WithCorrelationPayloadUris(IDictionary<Uri, string> payloadUrisWithDestinations);

        /// <summary>
        /// Adds whole directory as a correlation payload, archived.
        /// </summary>
        /// <param name="directory">
        /// The directory to be archived and sent as a correlation payload.
        /// </param>
        /// <param name="destination">
        /// The path on the target system for the correlation payload to be extracted.
        /// </param>
        /// <returns>Fluent job builder.</returns>
        IJobDefinition WithCorrelationPayloadDirectory(string directory, string destination = "");

        /// <summary>
        /// Adds whole directory as a correlation payload, archived.
        /// </summary>
        /// <param name="directory">Path to the directory.</param>
        /// <param name="includeDirectoryName">
        /// Should the resulting archive contain the folder with its
        /// content inside (`true`), or directly the content (`false`)?
        /// </param>
        /// <param name="destination">
        /// The path on the target system for the correlation payload to be extracted.
        /// </param>
        /// <returns>Fluent job builder.</returns>
        IJobDefinition WithCorrelationPayloadDirectory(string directory, bool includeDirectoryName, string destination = "");

        /// <summary>
        /// Adds whole directory as a correlation payload, archived.
        /// </summary>
        /// <param name="directory">Path to the directory.</param>
        /// <param name="archiveEntryPrefix">Prefix paths inside of the archive by arbitrary subpath.</param>
        /// <param name="destination">The path on the target system for the correlation payload to be extracted.</param>
        /// <returns>Fluent job builder.</returns>
        IJobDefinition WithCorrelationPayloadDirectory(string directory, string archiveEntryPrefix, string destination);

        /// <summary>
        /// Adds archive as a correlation payload.
        /// </summary>
        /// <param name="archive">Path to the archive.</param>
        /// <param name="destination">The path on the target system for the correlation payload to be extracted.</param>
        /// <returns>Fluent job builder.</returns>
        IJobDefinition WithCorrelationPayloadArchive(string archive, string destination = "");

        /// <summary>
        /// Adds ad-hoc specified list of files as a correlation payload.
        /// Directory structure won't be preserved.
        /// </summary>
        /// <param name="files">The files to archive and add as correlation payload.</param>
        /// <returns>Fluent job builder.</returns>
        IJobDefinition WithCorrelationPayloadFiles(params string[] files);

        /// <summary>
        /// Adds ad-hoc specified list of files as a correlation payload.
        /// Directory structure won't be preserved.
        /// </summary>
        /// <param name="files">Paths to archives.</param>
        /// <param name="destination">The path on the target system for the correlation payload to be extracted.</param>
        /// <returns>Fluent job builder.</returns>
        IJobDefinition WithCorrelationPayloadFiles(IList<string> files, string destination);

        /// <summary>
        /// Sets the Helix Source property.
        /// </summary>
        /// <returns>Fluent job builder.</returns>
        IJobDefinition WithSource(string source);

        /// <summary>
        /// Upserts a value into the dictionary of properties
        /// attached to the job. Helix infrastructure does not care
        /// about these properties and you can use them for your
        /// own ends.
        /// </summary>
        /// <returns>Fluent job builder.</returns>
        IJobDefinition WithProperty(string key, string value);

        /// <summary>
        /// For non-authenticated scenarios you have to provide information
        /// about creator of the job.
        /// 
        /// Beware using this for authenticated calls. The value is going
        /// to be provided by the user corresponding to your auth and
        /// an attempt to set anything else will make the job fail.
        /// </summary>
        /// <returns>Fluent job builder.</returns>
        IJobDefinition WithCreator(string creator);

        /// <summary>
        /// Sets custom container name for Azure blob storage to store the payload.
        /// </summary>
        /// <returns>Fluent job builder.</returns>
        IJobDefinition WithContainerName(string targetContainerName);

        /// <summary>
        /// Specifies custom connection string to Azure storage account of your choice.
        /// It will be used for storing blobs of payloads and job lists.
        /// </summary>
        /// <returns>Fluent job builder.</returns>
        IJobDefinition WithStorageAccountConnectionString(string accountConnectionString);

        /// <summary>
        /// Specifies name of the container for storage of results.
        /// </summary>
        /// <returns>Fluent job builder.</returns>
        IJobDefinition WithResultsContainerName(string resultsContainerName);

        /// <summary>
        /// Sets max automatic retry of workitems which do not return 0.
        /// </summary>
        /// <returns>Fluent job builder.</returns>
        IJobDefinition WithMaxRetryCount(int? maxRetryCount);

        /// <summary>
        /// Sends the fully specified job to execution.
        /// 
        /// This includes upload of all the provided correlation data, but does not
        /// include waiting for the task to fully execute in Helix.
        /// 
        /// You can use WaitAsync method of the ISentJob return value to wait for completion.
        /// </summary>
        /// <returns>Job accepted by Helix.</returns>
        Task<ISentJob> SendAsync(Action<string> log = null, CancellationToken cancellationToken = default);
    }
}
