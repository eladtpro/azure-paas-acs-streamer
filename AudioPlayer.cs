using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs.Extensions.Http;

namespace RadioArchive
{
	public class AudioPlayer
	{
        [FunctionName("AudioPlayer")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get")]
            HttpRequest req,
            ExecutionContext context,
            ILogger logger)
        {
            string path = Path.Combine(context.FunctionAppDirectory, "Resources", "AudioPlayerTemplate.html");
            logger.LogInformation($"[AudioPlayer] path: {path}, exists: {File.Exists(path)}");
            string html = await File.ReadAllTextAsync(path);
            return new ContentResult { Content = html, ContentType = "text/html" };
        }
    }
}

