namespace RadioArchive
{
    public class Settings
    {
        public const string Section = nameof(Settings);

        public string   AzureInputStorage               { get; set; }
        public bool     AutoProcessStreamingLocator     { get; set; }
        public string   MediaServicesAccountName        { get; set; }
        public string   ResourceGroup                   { get; set; }
        public string   SubscriptionId                  { get; set; }
        public double   ContainerSasExpiryHours         { get; set; }
        public string   DefaultStreamingEndpointName    { get; set; }
        public string   StreamingLocatorScheme          { get; set; }
        public string   AssetStorageAccountName         { get; set; }
        public bool     DeleteJobs                      { get; set; }
        public string   StreamingTransformName          { get; set; }
        public string   AzureMediaServicesScope         { get; set; }

        public override string ToString()
        {
            return $"AutoProcessStreamingLocator: {AutoProcessStreamingLocator}, MediaServicesAccountName: {MediaServicesAccountName}, ResourceGroup: {ResourceGroup},  SubscriptionId: {SubscriptionId}, ContainerSasExpiryHours: {ContainerSasExpiryHours}, AssetStorageAccountName: {AssetStorageAccountName}, DeleteJobs: {DeleteJobs}, StreamingTransformName: {StreamingTransformName}, AzureMediaServicesScope: {AzureMediaServicesScope}";
        }
    }
}

