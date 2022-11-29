using Microsoft.Azure.Management.Media.Models;
using Microsoft.Azure.Management.Media;
using Microsoft.Rest;
using Microsoft.Rest.Azure;
using Microsoft.Rest.Azure.OData;
using Azure.Identity;
using Azure.Core;

namespace RadioArchive
{
    public class StreamingLocatorGenerator : IStreamingLocatorGenerator
    {
        private readonly ISettings settings;
        private readonly ILogger<StreamingLocatorGenerator> logger;

        public StreamingLocatorGenerator(ISettings settings, ILogger<StreamingLocatorGenerator> logger)
        {
            this.settings = settings;
            this.logger = logger;
        }

        public async Task<IDictionary<string, StreamingPath>> Generate(LocatorRequest request)
        {
            string name = request.Name.Sanitize();
            try
            {
                logger.LogInformation($"[StreamingLocatorGenerator.Generate] CreateMediaServicesClientAsync {request}");
                IAzureMediaServicesClient client = await CreateMediaServicesClientAsync();
                logger.LogInformation($"GetStreamLocator name:{name}");
                StreamingLocator locator = await GetStreamLocator(client, name);
                if (null == locator)
                {
                    logger.LogInformation($"[StreamingLocatorGenerator.Generate] CreateInputAssetAsync name:{name}.input");
                    Asset input = await CreateInputAssetAsync(client, $"{name}.input", request.Blob);
                    logger.LogInformation($"[StreamingLocatorGenerator.Generate] CreateOutputAssetAsync name:{name}.output");
                    Asset output = await CreateOutputAssetAsync(client, $"{name}.output");
                    logger.LogInformation($"[StreamingLocatorGenerator.Generate] GetOrCreateTransformAsync");
                    Transform transform = await GetOrCreateTransformAsync(client);
                    logger.LogInformation($"[StreamingLocatorGenerator.Generate] SubmitJobAsync name: {name}");
                    Job job = await SubmitJobAsync(client, name, input, output);
                    logger.LogInformation($"[StreamingLocatorGenerator.Generate] WaitForJobToFinishAsync job: {job.Name}");
                    await WaitForJobToFinishAsync(job);
                    logger.LogInformation($"[StreamingLocatorGenerator.Generate] CreateStreamingLocatorAsync name: {name}");
                    locator = await CreateStreamingLocatorAsync(client, output, name);
                    logger.LogInformation($"[StreamingLocatorGenerator.Generate] CleanUp name: {name}");
                    await CleanUp(client, input, job);
                }
                logger.LogInformation($"[StreamingLocatorGenerator.Generate] GetStreamingUrlsAsync locator:{locator}");
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

        private async Task WaitForJobToFinishAsync(Job job)
        {
            const int SleepIntervalMs = 500;
            JobOutput jobOutput = job.Outputs.First(); //should be only one output
            do
            {
                if (JobState.Processing == jobOutput.State)
                    Console.Write($"\r{job.Name} - {jobOutput.State} {jobOutput.Progress}%");
                else if (JobState.Scheduled == job.State || JobState.Queued == job.State)
                    await Task.Delay(SleepIntervalMs);
                else
                    break;
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
                        Preset = new StandardEncoderPreset
                        {
                            Codecs = {new CopyAudio(), new AacAudio {
                                Channels = 2,
                                SamplingRate = 48000,
                                Bitrate = 128000,
                                Profile = AacAudioProfile.AacLc,
                                Label = "aac-lc"
                            } },
                            Formats = { new Mp4Format() }
                        }
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
            Asset parameters = new Asset
            {
                StorageAccountName = settings.AssetStorageAccountName
            };
            return await client.Assets.CreateOrUpdateAsync(settings.ResourceGroup, settings.MediaServicesAccountName, assetName, parameters);
        }

        private async Task<Asset> CreateInputAssetAsync(IAzureMediaServicesClient client, string assetName, Stream blob)
        {
            Asset parameters = new Asset
            {
                StorageAccountName = settings.AssetStorageAccountName

            };
            Asset asset = await client.Assets.CreateOrUpdateAsync(settings.ResourceGroup, settings.MediaServicesAccountName, assetName, parameters);
            Console.WriteLine($"Input Asset created {asset.Name}, modified: {asset.LastModified}");


            AssetContainerSas response = await client.Assets.ListContainerSasAsync(
                settings.ResourceGroup,
                settings.MediaServicesAccountName,
                asset.Name,
                permissions: AssetContainerPermission.ReadWrite,
                expiryTime: DateTime.UtcNow.AddHours(settings.ContainerSasExpiryHours).ToUniversalTime());

            Uri sasUri = new Uri(response.AssetContainerSasUrls.First());
            Console.WriteLine(sasUri);
            BlobContainerClient container = new BlobContainerClient(sasUri);
            BlobClient amsBlob = container.GetBlobClient(asset.Name);

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
                        // "https://management.core.windows.net" + "/.default",
                        //"Microsoft.Media/mediaServices" + "/.default"
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

