using Microsoft.AspNetCore.Mvc;

namespace RadioArchive
{
    public class AudioPlayerPage
	{
        [FunctionName("AudioPlayerPage")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequest req,
            [SignalRConnectionInfo(HubName = "sr-backend-channel", ConnectionStringSetting = "AzureSignalRConnectionString")] SignalRConnectionInfo connectionInfo,
            ExecutionContext context,
            ILogger logger)
        {
            
            logger.LogInformation("C# HTTP trigger function processed a request.");
            string path = Path.Combine(context.FunctionAppDirectory, "Resources", "AudioPlayerPageTemplate.html");
            logger.LogInformation($"[AudioPlayerPage] path: {path}, exists: {File.Exists(path)}");
            string template = await File.ReadAllTextAsync(path);
            string blobName = req.Query["name"];
            // string baseUrl = $"https://{Environment.GetEnvironmentVariable("WEBSITE_HOSTNAME")}";
            string html = template
                .Replace("{blobName}",blobName)
                .Replace("{{connectionUrl}}", connectionInfo.Url)
                .Replace("{{accessToken}}", connectionInfo.AccessToken);
            return new ContentResult { Content = html, ContentType = "text/html" };
        }
    }
}

