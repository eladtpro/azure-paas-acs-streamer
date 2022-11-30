using Microsoft.Azure.WebJobs.Extensions.SignalRService;

namespace RadioArchive
{
    public static class Notify
    {
        [FunctionName(nameof(Notify))]
        public static async Task Run(
            [ActivityTrigger] string message,
            [SignalR(HubName = "sr-backend-chunnel")] IAsyncCollector<SignalRMessage> signalRMessages)
        {
            await signalRMessages.AddAsync(
                new SignalRMessage
                {
                    Target = "notify",
                    Arguments = new[] { message }
                });
        }
    }
}
