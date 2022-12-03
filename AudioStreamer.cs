namespace RadioArchive
{
    public class AudioStreamer
    {
        private readonly Settings settings;
        private readonly ILogger logger;
        public AudioStreamer(IOptions<Settings> options, ILogger<AudioStreamer> logger)
        {
            this.settings = options.Value;
            this.logger = logger;
        }

        [Timeout("10:00:00")]
        [FunctionName("AudioStreamer")]
        public async Task<HttpResponseMessage> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequest request,
            [Blob("data/{Query.name}", FileAccess.Read, Connection = "Settings__AzureInputStorage")] BlobClient blob,
            [DurableClient] IDurableOrchestrationClient starter,
            [SignalR(HubName = "sr-backend-chunnel")] IAsyncCollector<SignalRMessage> signalRMessages)
        {
            logger.LogInformation("[AudioStreamer] C# HTTP trigger function processed a request.");
            logger.LogInformation($"[AudioStreamer] CreateMediaServicesClientAsync token: {settings}, Blob name {blob.Name}, blob length {blob}");

            IDictionary<string, StreamingPath> urls = new Dictionary<string, StreamingPath>();
            BlobProperties props = await blob.GetPropertiesAsync();

            // if (ContentType.Audio == props.ContentType.ResolveType()){
            //     LocatorContext locator = new LocatorContext(blob.Name, props);
            //     string result = await starter.StartNewAsync<LocatorContext>(nameof(StreamingLocatorGenerator), locator);
            // }


            // TEST TEST TEST
            LocatorContext locator = new LocatorContext(blob.Name, props);
            string result = await starter.StartNewAsync<LocatorContext>(nameof(Mocker), locator);


            await signalRMessages.AddAsync(
                new SignalRMessage
                {
                    Target = "notify",
                    Arguments = new[] { "intializing", "some description" }
                });

            logger.LogInformation($"[AudioStreamer] urls: {urls}");

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonConvert.SerializeObject(urls), Encoding.UTF8, "application/json")
            };
        }
    }
}

