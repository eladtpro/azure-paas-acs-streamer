namespace RadioArchive
{
    public class Tokenizer
    {
        [FunctionName(nameof(Tokenizer))]
        public async Task<LocatorContext> Run(
        [ActivityTrigger] LocatorContext locator,
        ISettings settings,
        ILogger logger)
        {
            logger.LogInformation($"[Tokenizer] C# ActivityTrigger trigger function Processed locator:{locator}");
            IAzureMediaServicesClient client = await CreateMediaServicesClientAsync(settings, logger);
            AssetContainerSas response = await client.Assets.ListContainerSasAsync(
                settings.ResourceGroup,
                settings.MediaServicesAccountName,
                $"{locator.Name}.input",
                permissions: AssetContainerPermission.ReadWrite,
                expiryTime: DateTime.UtcNow.AddHours(settings.ContainerSasExpiryHours).ToUniversalTime());
            locator.SasUri =  new Uri(response.AssetContainerSasUrls.First());

            return locator;
        }

        private async Task<IAzureMediaServicesClient> CreateMediaServicesClientAsync(
            ISettings settings,
            ILogger logger)
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