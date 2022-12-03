using Microsoft.AspNetCore.Mvc;

namespace RadioArchive
{
    public class AudioPlayer
	{
        [FunctionName("AudioPlayer")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get")]
            [SignalRConnectionInfo(HubName = "sr-backend-chunnel.service.signalr.net")] SignalRConnectionInfo connectionInfo,
            HttpRequest req,
            ExecutionContext context,
            ILogger logger)
        {
            
            logger.LogInformation("C# HTTP trigger function processed a request.");
            string path = Path.Combine(context.FunctionAppDirectory, "Resources", "AudioPlayerTemplate.html");
            logger.LogInformation($"[AudioPlayer] path: {path}, exists: {File.Exists(path)}");
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

