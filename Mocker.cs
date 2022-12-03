using Microsoft.AspNetCore.Mvc;

namespace RadioArchive;
public static class Mocker
{
    [FunctionName(nameof(Mocker))]
    public static async Task<IActionResult> RunOrchestrator(
        [OrchestrationTrigger] IDurableOrchestrationContext context)
    {
        LocatorContext request = context.GetInput<LocatorContext>();
        request.Created = context.CurrentUtcDateTime;
        string message = $"Test Message {context.CurrentUtcDateTime}";
        await context.CallActivityAsync<Task>(nameof(Notify), message);

        return new ContentResult { Content = message, ContentType = "text/html" };
    }
}

