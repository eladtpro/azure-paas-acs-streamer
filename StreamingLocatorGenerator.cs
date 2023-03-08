using System.Threading;
using Microsoft.Azure.Management.Media.Models;

namespace RadioArchive
{
    public class StreamingLocatorGenerator
    {
        private readonly Settings settings;
        private readonly ILogger<StreamingLocatorGenerator> logger;

        public StreamingLocatorGenerator(IOptions<Settings> options, ILogger<StreamingLocatorGenerator> logger)
        {
            this.settings = options.Value;
            this.logger = logger;
        }

        [FunctionName(nameof(StreamingLocatorGenerator))]
        public async Task<IDictionary<string, StreamingPath>> Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            LocatorContext request = context.GetInput<LocatorContext>();
            request.Created = context.CurrentUtcDateTime;
            
            string name = request.Name;
            try
            {
                request = await context.CallActivityAsync<LocatorContext>(nameof(Tokenizer), request);
                logger.LogInformation($"[StreamingLocatorGenerator.Generate] CreateMediaServicesClientAsync {request}");
                IAzureMediaServicesClient client = await CreateMediaServicesClientAsync();
                logger.LogInformation($"GetStreamLocator name:{name}");
                StreamingLocator locator = await GetStreamLocator(client, name);
                if (null == locator)
                {
                    Asset input = await CreateInputAssetAsync(client, $"{name}.input", request);
                    Asset output = await CreateOutputAssetAsync(client, $"{name}.output");
                    Transform transform = await GetOrCreateTransformAsync(client);
                    Job job = await SubmitJobAsync(client, name, input, output);
                    await WaitForJobToFinishAsync(job, context);
                    locator = await CreateStreamingLocatorAsync(client, output, name);
                    await CleanUp(client, input, job);
                }
                logger.LogInformation($"[StreamingLocatorGenerator.Generate] Success locator:{locator}, request:{request}");
                return await GetStreamingUrlsAsync(client, locator);
            }
            catch (ErrorResponseException ex)
            {
                logger.LogError(ex, $"[StreamingLocatorGenerator.Generate] Error (Response):{ex.Body.Error.Message}, request:{request}");
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"[StreamingLocatorGenerator.Generate] Error:{ex.Message}, request:{request}");
                throw;
            }
        }

        private async Task<StreamingLocator> GetStreamLocator(IAzureMediaServicesClient client, string name)
        {
            ODataQuery<StreamingLocator> query = new ODataQuery<StreamingLocator>((loc) => loc.Name == name);
            logger.LogInformation($"lient.StreamingLocators.ListAsync({settings.ResourceGroup}, {settings.MediaServicesAccountName}, {query})");
            IPage<StreamingLocator> locators = await client.StreamingLocators.ListAsync(settings.ResourceGroup, settings.MediaServicesAccountName, query);
            StreamingLocator locator = locators.FirstOrDefault();
            logger.LogInformation($"Locator {name}: {locator}");
            return locator;
        }

        private async Task CleanUp(IAzureMediaServicesClient client, Asset input, Job job)
        {
            logger.LogInformation($"[StreamingLocatorGenerator.Generate] CleanUp job: {job.Name}");
            try
            {
                await client.Assets.DeleteAsync(settings.ResourceGroup, settings.MediaServicesAccountName, input.Name);
                if (settings.DeleteJobs)
                    await client.Jobs.DeleteAsync(settings.ResourceGroup, settings.MediaServicesAccountName, settings.StreamingTransformName, job.Name);

            }
            catch (Exception ex)
            {
                logger.LogError(ex, ex.Message);
            }
        }

        private async Task<IDictionary<string, StreamingPath>> GetStreamingUrlsAsync(IAzureMediaServicesClient client, StreamingLocator locator)
        {
            logger.LogInformation($"[StreamingLocatorGenerator.Generate] GetStreamingUrlsAsync locator:{locator}");

            IDictionary<string, StreamingPath> streamingUrls = new Dictionary<string, StreamingPath>();

            StreamingEndpoint streamingEndpoint = await client.StreamingEndpoints.GetAsync(settings.ResourceGroup, settings.MediaServicesAccountName, settings.DefaultStreamingEndpointName);

            if (streamingEndpoint != null)
            {
                if (streamingEndpoint.ResourceState != StreamingEndpointResourceState.Running)
                {
                    await client.StreamingEndpoints.StartAsync(settings.ResourceGroup, settings.MediaServicesAccountName, settings.DefaultStreamingEndpointName);
                }
            }

            ListPathsResponse paths = await client.StreamingLocators.ListPathsAsync(settings.ResourceGroup, settings.MediaServicesAccountName, locator.Name);

            foreach (StreamingPath path in paths.StreamingPaths)
            {
                UriBuilder uriBuilder = new UriBuilder
                {
                    Scheme = settings.StreamingLocatorScheme,
                    Host = streamingEndpoint.HostName,
                    Path = path.Paths[0]
                };
                streamingUrls[uriBuilder.ToString()] = path;
            }

            return streamingUrls;
        }

        private async Task<StreamingLocator> CreateStreamingLocatorAsync(IAzureMediaServicesClient client, Asset output, string name)
        {
            logger.LogInformation($"[StreamingLocatorGenerator.Generate] CreateStreamingLocatorAsync name: {name}");
            ODataQuery<StreamingLocator> query = new ODataQuery<StreamingLocator>((loc) => loc.Name == name);
            IPage<StreamingLocator> locators = await client.StreamingLocators.ListAsync(settings.ResourceGroup, settings.MediaServicesAccountName, query);

            StreamingLocator locator = locators.FirstOrDefault();

            locator ??= await client.StreamingLocators.CreateAsync(
                settings.ResourceGroup,
                settings.MediaServicesAccountName,
                name,
                new StreamingLocator
                {
                    AssetName = output.Name,
                    StreamingPolicyName = PredefinedStreamingPolicy.ClearStreamingOnly
                });

            return locator;
        }

        private async Task WaitForJobToFinishAsync(Job job, IDurableOrchestrationContext context)
        {
            logger.LogInformation($"[StreamingLocatorGenerator.Generate] WaitForJobToFinishAsync job: {job.Name}");
            
            // const int SleepIntervalMs = 500;
            JobOutput jobOutput = job.Outputs.First(); //should be only one output
            do
            {
                if (JobState.Processing == jobOutput.State)
                {
                    string message = $"\r{job.Name} - {jobOutput.State} {jobOutput.Progress}%";
                    await context.CallActivityAsync<Task>(nameof(Notify), $"Processing: \r{job.Name} - {jobOutput.State} {jobOutput.Progress}%");
                    Console.Write(message);
                }
                else if (JobState.Scheduled == job.State || JobState.Queued == job.State)
                    await context.CallActivityAsync<Task>(nameof(Notify), $"Waiting: \r{job.Name} - {jobOutput.State} {jobOutput.Progress}%");
                else
                    break;
                // Orchestration sleeps until this time.
                var nextCheck = context.CurrentUtcDateTime.AddSeconds(2);
                await context.CreateTimer(nextCheck, CancellationToken.None);
            }
            while (
                job.State != JobState.Finished &&
                job.State != JobState.Error &&
                job.State != JobState.Canceled &&
                job.State != JobState.Canceling);

            Console.Write($"{job.Name} has {jobOutput.State}");
        }

        private async Task<Job> SubmitJobAsync(IAzureMediaServicesClient client, string jobName, Asset input, Asset output, TimeSpan? start = null, TimeSpan? end = null)
        {
            logger.LogInformation($"[StreamingLocatorGenerator.Generate] SubmitJobAsync jobName: {jobName}");
            ODataQuery<Job> query = new ODataQuery<Job>(j => j.Name == jobName);
            IPage<Job> jobs = await client.Jobs.ListAsync(settings.ResourceGroup, settings.MediaServicesAccountName, settings.StreamingTransformName, query);
            Job job = jobs.FirstOrDefault();



            ClipTime clipStart = (null != start) ? new AbsoluteClipTime(start.Value) : null;
            ClipTime clipEnd = (null != end) ? new AbsoluteClipTime(end.Value) : null;

            job ??= await client.Jobs.CreateAsync(
                settings.ResourceGroup,
                settings.MediaServicesAccountName,
                settings.StreamingTransformName,
                jobName,
                new Job
                {
                    Input = new JobInputAsset(input.Name, start: clipStart, end: clipEnd),
                    Outputs = new[]{
                        new JobOutputAsset(output.Name),
                    },
                });

            return job;
        }

        private async Task<Transform> GetOrCreateTransformAsync(IAzureMediaServicesClient client)
        {
            logger.LogInformation($"[GetOrCreateTransformAsync]");

            // Does a Transform already exist with the desired name? Assume that an existing Transform with the desired name
            // also uses the same recipe or Preset for processing content.
            Transform transform = null;

            try
            {
                transform = await client.Transforms.GetAsync(settings.ResourceGroup, settings.MediaServicesAccountName, settings.StreamingTransformName);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, $"[GetOrCreateTransformAsync] Could not fetch Transform. Details: client.Transforms.GetAsync({settings.ResourceGroup}, {settings.MediaServicesAccountName}, {settings.StreamingTransformName})");
            }


            if (transform == null)
            {
                // You need to specify what you want it to produce as an output
                TransformOutput[] output = new TransformOutput[]
                {
                    new TransformOutput
                    {
                        Preset = PresetFactory.Preset(PresetProfile.AacAudio)
                    }
                };

                logger.LogInformation($"[GetOrCreateTransformAsync] output: {output}");

                // Create the Transform with the output defined above
                transform = await client.Transforms.CreateOrUpdateAsync(settings.ResourceGroup, settings.MediaServicesAccountName, settings.StreamingTransformName, output);
            }

            return transform;
        }

        private async Task<Asset> CreateOutputAssetAsync(IAzureMediaServicesClient client, string assetName)
        {
            logger.LogInformation($"[CreateOutputAssetAsync] assetName:{assetName}.output");
            Asset parameters = new Asset
            {
                StorageAccountName = settings.AssetStorageAccountName
            };
            return await client.Assets.CreateOrUpdateAsync(settings.ResourceGroup, settings.MediaServicesAccountName, assetName, parameters);
        }

        private async Task<Asset> CreateInputAssetAsync(IAzureMediaServicesClient client, string assetName, LocatorContext request)
        {
            logger.LogInformation($"[CreateInputAssetAsync] assetName:{assetName}");
            BlobContainerClient sourceContainerClient = new BlobContainerClient(settings.AzureInputStorage, LocatorContext.ContainerName);
            MemoryStream blob = new MemoryStream();
            await sourceContainerClient.GetBlobClient(request.OriginalName).DownloadToAsync(blob);;
            // BlobClient blobClient = sourceContainerClient.GetBlobClient(request.OriginalName);
            Asset parameters = new Asset
            {
                StorageAccountName = settings.AssetStorageAccountName
            };
            Asset asset = await client.Assets.CreateOrUpdateAsync(settings.ResourceGroup, settings.MediaServicesAccountName, assetName, parameters);
            Console.WriteLine($"Input Asset created {asset.Name}, modified: {asset.LastModified}, sasUri: {request.SasUri}");
            BlobContainerClient targetContainerClient = new BlobContainerClient(request.SasUri);
            BlobClient amsBlob = targetContainerClient.GetBlobClient(asset.Name);

            //Initialize a progress handler. When the file is being uploaded, the current uploaded bytes will be published back to us using this progress handler by the Blob Storage Service
            long length = blob.Length;
            Progress<long> progress = new Progress<long>();
            progress.ProgressChanged += (s, current) =>
            {
                Console.Write($"\r{asset.Name} - Uploading {(100 * current / length)}%");
            };

            BlobUploadOptions options = new BlobUploadOptions
            {
                ProgressHandler = progress, //Make sure to pass the progress handler here
                AccessTier = AccessTier.Hot,
            };

            // Use Strorage API to upload the file into the container in storage.
            blob.Position = 0;
            BlobContentInfo info = await amsBlob.UploadAsync(blob, options);
            Console.WriteLine();
            Console.WriteLine($"BlobContentInfo SequenceNumber: {info.BlobSequenceNumber}, ETag: {info.ETag}, VersionId: {info.VersionId}");
            return asset;
        }

        /// <summary>
        /// Creates the AzureMediaServicesClient object based on the credentials
        /// supplied in local configuration file.
        /// </summary>
        /// <param name="config">The parm is of type ConfigWrapper. This class reads values from local configuration file.</param>
        /// <returns></returns>
        // <CreateMediaServicesClient>
        private async Task<IAzureMediaServicesClient> CreateMediaServicesClientAsync()
        {
            //var credentials = await GetCredentialsAsync(config);
            logger.LogInformation($"CreateMediaServicesClientAsync trying to get token");
            ManagedIdentityCredential credential = new ManagedIdentityCredential();
            var accessTokenRequest = await credential.GetTokenAsync(
                new TokenRequestContext(
                    scopes: new string[] {
                        settings.AzureMediaServicesScope
                        // "https://management.core.windows.net/.default"
                        }
                    )
                );
            logger.LogInformation($"CreateMediaServicesClientAsync token: {accessTokenRequest}");

            ServiceClientCredentials credentials = new TokenCredentials(accessTokenRequest.Token, "Bearer");

            return new AzureMediaServicesClient(credentials)
            {
                SubscriptionId = settings.SubscriptionId
            };
        }
    }
}

