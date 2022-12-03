namespace RadioArchive
{
    public class Tokenizer
    {
        [FunctionName(nameof(Tokenizer))]
        public static async Task<LocatorContext> Run(
        [ActivityTrigger] LocatorContext request,
        ILogger<Tokenizer> logger,
        IOptions<Settings> options)
        {
            Settings settings = options.Value;
            logger.LogInformation($"[Tokenizer] C# ActivityTrigger trigger function Processed locator:{request}");
            IAzureMediaServicesClient client = await CreateMediaServicesClientAsync(settings, logger);
            AssetContainerSas response = await client.Assets.ListContainerSasAsync(
                settings.ResourceGroup,
                settings.MediaServicesAccountName,
                $"{request.Name}.input",
                permissions: AssetContainerPermission.ReadWrite,
                expiryTime: request.Created.AddHours(settings.ContainerSasExpiryHours).ToUniversalTime());
            request.SasUri =  new Uri(response.AssetContainerSasUrls.First());

            return request;
        }

        private static async Task<IAzureMediaServicesClient> CreateMediaServicesClientAsync(
            Settings settings,
            ILogger<Tokenizer> logger)
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