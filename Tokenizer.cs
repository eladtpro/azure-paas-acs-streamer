namespace RadioArchive
{
    public class Tokenizer
    {
        private readonly Settings settings;
        private readonly ILogger logger;
        public Tokenizer(IOptions<Settings> options, ILogger<Tokenizer> logger)
        {
            settings = options.Value;
            this.logger = logger;
        }

        [FunctionName(nameof(Tokenizer))]
        public async Task<LocatorContext> Run(
        [ActivityTrigger] LocatorContext request)
        {
            logger.LogInformation($"[Tokenizer] C# ActivityTrigger trigger function Processed locator:{request}");
            IAzureMediaServicesClient client = await CreateMediaServicesClientAsync();
            AssetContainerSas response = await client.Assets.ListContainerSasAsync(
                settings.ResourceGroup,
                settings.MediaServicesAccountName,
                $"{request.Name}.input",
                permissions: AssetContainerPermission.ReadWrite,
                expiryTime: request.Created.AddHours(settings.ContainerSasExpiryHours).ToUniversalTime());
            request.SasUri =  new Uri(response.AssetContainerSasUrls.First());

            return request;
        }

        private async Task<IAzureMediaServicesClient> CreateMediaServicesClientAsync()
        {
            //var credentials = await GetCredentialsAsync(config);
            logger.LogInformation($"CreateMediaServicesClientAsync trying to get token");
            ManagedIdentityCredential credential = new ManagedIdentityCredential();
            var accessTokenRequest = await credential.GetTokenAsync(
                new TokenRequestContext(scopes: new string[] {settings.AzureMediaServicesScope}));
            logger.LogInformation($"CreateMediaServicesClientAsync token: {accessTokenRequest}");

            ServiceClientCredentials credentials = new TokenCredentials(accessTokenRequest.Token, "Bearer");

            return new AzureMediaServicesClient(credentials)
            {
                SubscriptionId = settings.SubscriptionId
            };
        }

    }    
}