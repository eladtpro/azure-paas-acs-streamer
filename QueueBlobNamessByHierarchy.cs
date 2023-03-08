using Azure;

namespace RadioArchive
{
    public class QueueBlobNamessByHierarchy
    {
        [FunctionName("QueueBlobNamessByHierarchy")]
        public static async Task Run(
            [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequest req,
            [Blob("data/{Query.name}", FileAccess.Read, Connection = "AzureInputStorage")] BlobContainerClient client,
            [ServiceBus("queuename1", Connection = "ServiceBusConnectionString1")] IAsyncCollector<dynamic> outputList,
            IOptions<Settings> options,
            ILogger logger)
        {
            //get all storage account container file names hierarchically
            string containerName = req.Query["name"];
            string prefix = req.Query["prefix"];
            await ListBlobsHierarchicalListing(client, prefix, outputList, options.Value.ListBlobsSegmentSize);
        }

        private static async Task ListBlobsHierarchicalListing(BlobContainerClient container, string prefix, IAsyncCollector<dynamic> outputList, int? segmentSize)
        {
            try
            {
                // Call the listing operation and return pages of the specified size.
                var resultSegment = container.GetBlobsByHierarchyAsync(prefix: prefix, delimiter: "/")
                    .AsPages(default, segmentSize);

                // Enumerate the blobs returned for each page.
                await foreach (Page<BlobHierarchyItem> blobPage in resultSegment)
                {
                    // A hierarchical listing may return both virtual directories and blobs.
                    foreach (BlobHierarchyItem blobhierarchyItem in blobPage.Values)
                    {
                        if (blobhierarchyItem.IsPrefix)
                        {
                            // Write out the prefix of the virtual directory.
                            Console.WriteLine("Virtual directory prefix: {0}", blobhierarchyItem.Prefix);

                            await outputList.AddAsync(blobhierarchyItem.Blob.Name);
                            // Call recursively with the prefix to traverse the virtual directory.
                            await ListBlobsHierarchicalListing(container, blobhierarchyItem.Prefix, outputList, null);
                        }
                        else
                        {
                            // Write out the name of the blob.
                            Console.WriteLine("Blob name: {0}", blobhierarchyItem.Blob.Name);
                        }
                    }

                    Console.WriteLine();
                }
            }
            catch (RequestFailedException e)
            {
                Console.WriteLine(e.Message);
                Console.ReadLine();
                throw;
            }
        }
    }
}

