namespace RadioArchive;
public static class Negotiate
{
    [FunctionName(nameof(Negotiate))]
    public static SignalRConnectionInfo Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = null)] HttpRequest req,
        [SignalRConnectionInfo(HubName = "sr-backend-chunnel.service.signalr.net")] SignalRConnectionInfo connectionInfo,
        ILogger log)
    {
        return connectionInfo;
    }
}