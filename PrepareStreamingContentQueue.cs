namespace RadioArchive;

public class PrepareStreamingContentQueue
{
    [FunctionName("ServiceBusQueueTriggerCSharp")]
    public async static Task Run(
        [ServiceBusTrigger("myqueue", Connection = "ServiceBusConnection")]
            string myQueueItem,
        Int32 deliveryCount,
        DateTime enqueuedTimeUtc,
        string messageId,
        [Blob("files/{myQueueItem}", FileAccess.Read)] BlobClient blob, string name,
        IOptions<Settings> options,
        [DurableClient] IDurableOrchestrationClient starter,
        ILogger logger)
    {

        logger.LogInformation($"PrepareStreamingContent: C# Blob trigger function Processed blob\n Name:{name}");
        if (options.Value.AutoProcessStreamingLocator)
            return;

        BlobProperties props = await blob.GetPropertiesAsync();
        if (ContentType.Audio == props.ContentType.ResolveType())
        {
            LocatorContext locator = new LocatorContext(name, props);
            await starter.StartNewAsync<LocatorContext>(nameof(StreamingLocatorGenerator), locator);
        }
    }
}
