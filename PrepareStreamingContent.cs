namespace RadioArchive
{
    public class PrepareStreamingContent
    {
        [FunctionName(nameof(PrepareStreamingContent))]
        public static async Task Run(
        [BlobTrigger("data/{name}", Connection = "Settings:AzureInputStorage")] BlobClient blob, string name,
        [DurableClient] IDurableOrchestrationClient starter,
        IOptions<Settings> options, 
        IStreamingLocatorGenerator generator, 
        ILogger<PrepareStreamingContent> logger)
        {
            Settings settings = options.Value;
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