namespace RadioArchive
{
    public class PrepareStreamingContent
    {
        private readonly ISettings settings;
        private readonly ILogger<PrepareStreamingContent> logger;
        private readonly IStreamingLocatorGenerator generator;

        public PrepareStreamingContent(ISettings settings, IStreamingLocatorGenerator generator, ILogger<PrepareStreamingContent> logger)
        {
            this.generator = generator;
            this.settings = settings;
            this.logger = logger;
        }

        [FunctionName("PrepareStreamingContent")]
        public async Task Run(
        [BlobTrigger("data/{name}", Connection = "AzureInputStorage")] CloudBlockBlob blob, string name)
        {
            logger.LogInformation($"PrepareStreamingContent: C# Blob trigger function Processed blob\n Name:{name}");
            if (settings.AutoProcessStreamingLocator)
                if (ContentType.Audio == blob.Properties.ContentType.ResolveType())
                {
                    using Stream stream = await blob.OpenReadAsync();
                    await generator.Generate(new LocatorRequest(name, stream));
                }
        }
    }
}