﻿using System;
namespace RadioArchive
{
    public interface ISettings
    {
        string AzureWebJobsStorage { get; set; }
        bool AutoProcessStreamingLocator { get; set; }
        public string MediaServicesAccountName { get; set; }
        public string ResourceGroup { get; set; }
        public string SubscriptionId { get; set; }
        public double ContainerSasExpiryHours { get; set; }
        public string DefaultStreamingEndpointName { get; set; }
        public string StreamingLocatorScheme { get; set; }
        public string AssetStorageAccountName { get; set; }
        public bool DeleteJobs { get; set; }
        public string StreamingTransformName { get; set; }
        public string AzureMediaServicesScope { get; set; }
    }
}

