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
            [HttpTrigger(AuthorizationLevel.Function, "get",
            Route = "hello_html")]
            HttpRequest req,
            ILogger log)
        {
            string html = await File.ReadAllTextAsync("AudioPlayerTemplate.html");
            return new ContentResult { Content = "<html><body>Hello <b>world</b></body></html>", ContentType = "text/html" };
        }
    }
}

