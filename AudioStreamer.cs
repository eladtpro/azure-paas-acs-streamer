using System.Net;
using System.Net.Http;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Management.Media.Models;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Newtonsoft.Json;

namespace RadioArchive
{
    public class AudioStreamer
    {
        private readonly ISettings settings;
        private readonly ILogger<AudioStreamer> logger;
        private readonly IStreamingLocatorGenerator generator;


        public AudioStreamer(ISettings settings, IStreamingLocatorGenerator generator, ILogger<AudioStreamer> logger)
        {
            this.settings = settings;
            this.generator = generator;
            this.logger = logger;
        }


        [FunctionName("AudioStreamer")]
        public async Task<HttpResponseMessage> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = null)] HttpRequest request,
            [Blob("data/{Query.name}", FileAccess.Read, Connection = "AzureInputStorage")] Stream blob)
        {
            logger.LogInformation("[AudioStreamer] C# HTTP trigger function processed a request.");
            logger.LogInformation($"[AudioStreamer] CreateMediaServicesClientAsync token: {settings}");

            string name = request.Query["name"];
            logger.LogInformation($"[AudioStreamer] Blob name {name}, blob length {blob.Length}");

            IDictionary<string, StreamingPath> urls = await generator.Generate(name, blob);
            logger.LogInformation($"[AudioStreamer] urls: {urls}");

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonConvert.SerializeObject(urls), Encoding.UTF8, "application/json")
            };
        }
    }
}

