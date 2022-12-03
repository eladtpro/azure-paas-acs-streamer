namespace RadioArchive
{
    public class PrepareStreamingContent
    {
        private readonly Settings settings;
        private readonly ILogger logger;
        public PrepareStreamingContent(IOptions<Settings> options, ILogger<PrepareStreamingContent> logger)
        {
            settings = options.Value;
            this.logger = logger;
        }

        [FunctionName(nameof(PrepareStreamingContent))]
        public async Task Run(
        [BlobTrigger("data/{name}", Connection = "AzureInputStorage")] BlobClient blob, string name,
        [DurableClient] IDurableOrchestrationClient starter)
        {
            logger.LogInformation($"PrepareStreamingContent: C# Blob trigger function Processed blob\n Name:{name}");
            if (settings.AutoProcessStreamingLocator)
                return;

            BlobProperties props = await blob.GetPropertiesAsync();
            if (ContentType.Audio == props.ContentType.ResolveType())
            {
                LocatorContext locator = new LocatorContext(name, props);
                await starter.StartNewAsync<LocatorContext>(nameof(StreamingLocatorGenerator), locator);
            }
        }
    }
}