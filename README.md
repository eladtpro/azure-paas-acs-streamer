Develop Azure Functions with Media Services v3
==============================================

-   Article
-   08/19/2022
-   4 minutes to read
-   2 contributors

Feedback

![Media Services logo v3](https://learn.microsoft.com/en-us/azure/media-services/latest/media/media-services-api-logo/azure-media-services-logo-v3.svg)

* * * * *

[AMS website](https://media.microsoft.com/) | [Media Services v2 documentation](https://learn.microsoft.com/en-us/azure/media-services/previous/media-services-overview) | [Code Samples](https://learn.microsoft.com/en-us/azure/media-services/latest/samples-overview?amspage=header) | [Troubleshooting guide](https://learn.microsoft.com/en-us/azure/media-services/latest/troubleshooting?amspage=header)

This article shows you how to get started with creating Azure Functions that use Media Services. The Azure Function in the associated sample for this article encodes a video file with Media Encoder Standard. As soon as the encoding job has been created, the function returns the job name and output asset name. To review Azure Functions, see [Overview](https://learn.microsoft.com/en-us/azure/azure-functions/functions-overview) and other topics in the Azure Functions section.

If you want to explore and deploy existing Azure Functions that use Azure Media Services, check out [Media Services Azure Functions](https://github.com/Azure-Samples/media-services-v3-dotnet-core-functions-integration). This repository contains examples that use Media Services to show workflows related to ingesting content directly from blob storage, encoding, and live streaming operations.

[](https://learn.microsoft.com/en-us/azure/media-services/latest/integrate-azure-functions-dotnet-how-to#prerequisites)Prerequisites
------------------------------------------------------------------------------------------------------------------------------------

-   Before you can create your first function, you need to have an active Azure account. If you don't already have an Azure account, [free accounts are available](https://azure.microsoft.com/free/).
-   If you are going to create Azure Functions that perform actions on your Azure Media Services (AMS) account or listen to events sent by Media Services, you should create an AMS account, as described [here](https://learn.microsoft.com/en-us/azure/media-services/latest/account-create-how-to).
-   Install [Visual Studio Code](https://code.visualstudio.com/) on one of the [supported platforms](https://code.visualstudio.com/docs/supporting/requirements#_platforms).

This article explains how to create a C# .NET 5 function that communicates with Azure Media Services. To create a function with another language, look to this [article](https://learn.microsoft.com/en-us/azure/azure-functions/functions-develop-vs-code).

[](https://learn.microsoft.com/en-us/azure/media-services/latest/integrate-azure-functions-dotnet-how-to#azure-functions-in-visual-studio-code)Azure Functions in Visual Studio Code
------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

Follow the steps for setting up and using the [Azure Functions extension](https://learn.microsoft.com/en-us/azure/azure-functions/functions-develop-vs) in Visual Studio Code, using the *Isolated process* steps.

When you have a project set up, come back to this page.

[](https://learn.microsoft.com/en-us/azure/media-services/latest/integrate-azure-functions-dotnet-how-to#generated-project-files)Generated project files
--------------------------------------------------------------------------------------------------------------------------------------------------------

The project template creates a project in your chosen language and installs required dependencies. The new project has these files:

-   host.json: Lets you configure the Functions host. These settings apply when you're running functions locally and when you're running them in Azure. For more information, see [host.json reference](https://learn.microsoft.com/en-us/azure/azure-functions/functions-host-json).

-   local.settings.json: Maintains settings used when you're running functions locally. These settings are used only when you're running functions locally.

     Important

    Because the local.settings.json file can contain secrets, you need to exclude it from your project source control.

-   HttpTriggerEncode.cs class file that implements the function.

### [](https://learn.microsoft.com/en-us/azure/media-services/latest/integrate-azure-functions-dotnet-how-to#httptriggerencodecs)HttpTriggerEncode.cs

This is the C# code for your function. Its role is to take a Media Services asset or a source URL and launches an encoding job with Media Services. It uses a Transform that is created if it does not exist. When it is created, it used the preset provided in the input body.

 Important

Replace the full content of HttpTriggerEncode.cs file with [`HttpTriggerEncode.cs` from this repository](https://github.com/Azure-Samples/media-services-v3-dotnet-core-functions-integration/blob/main/Tutorial/HttpTriggerEncode.cs).

Once you are done defining your function, select Save and Run.

The source code for the Run method of the function is:

C#Copy

```
[Function("HttpTriggerEncode")]
public static async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequestData req,
    FunctionContext executionContext)
{
    var log = executionContext.GetLogger("SubmitEncodingJob");
    log.LogInformation("C# HTTP trigger function processed a request.");

    // Get request body data.
    string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
    var data = (RequestBodyModel)JsonConvert.DeserializeObject(requestBody, typeof(RequestBodyModel));

    // Return bad request if input asset name is not passed in
    if (data.InputAssetName == null && data.InputUrl == null)
    {
        return ResponseBadRequest(req, "Please pass asset name or input Url in the request body");
    }

    // Return bad request if input asset name is not passed in
    if (data.TransformName == null)
    {
        return ResponseBadRequest(req, "Please pass the transform name in the request body");
    }

    ConfigWrapper config = new(new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
        .AddEnvironmentVariables() // parses the values from the optional .env file at the solution root
        .Build());

    IAzureMediaServicesClient client;
    try
    {
        client = await CreateMediaServicesClientAsync(config);
        log.LogInformation("AMS Client created.");
    }
    catch (Exception e)
    {
        if (e.Source.Contains("ActiveDirectory"))
        {
            log.LogError("TIP: Make sure that you have filled out the appsettings.json file before running this sample.");
        }
        log.LogError($"{e.Message}");
        return ResponseBadRequest(req, e.Message);
    }

    // Set the polling interval for long running operations to 2 seconds.
    // The default value is 30 seconds for the .NET client SDK
    client.LongRunningOperationRetryTimeout = 2;

    // Creating a unique suffix so that we don't have name collisions if you run the sample
    // multiple times without cleaning up.
    string uniqueness = Guid.NewGuid().ToString().Substring(0, 13);
    string jobName = $"job-{uniqueness}";
    string outputAssetName = $"output-{uniqueness}";

    Transform transform;
    try
    {
        // Ensure that you have the encoding Transform.  This is really a one time setup operation.
        transform = await CreateEncodingTransform(client, log, config.ResourceGroup, config.AccountName, data.TransformName, data.BuiltInPreset);
        log.LogInformation("Transform retrieved.");
    }
    catch (Exception e)
    {
        log.LogError("Error when creating/getting the transform.");
        log.LogError($"{e.Message}");
        return ResponseBadRequest(req, e.Message);
    }

    Asset outputAsset;
    try
    {
        // Output from the job must be written to an Asset, so let's create one
        outputAsset = await CreateOutputAssetAsync(client, log, config.ResourceGroup, config.AccountName, outputAssetName, data.OutputAssetStorageAccount);
        log.LogInformation($"Output asset '{outputAssetName}' created.");
    }
    catch (Exception e)
    {
        log.LogError("Error when creating the output asset.");
        log.LogError($"{e.Message}");
        return ResponseBadRequest(req, e.Message);
    }

    // Job input prepration : asset or url
    JobInput jobInput;
    if (data.InputUrl != null)
    {
        jobInput = new JobInputHttp(files: new[] { data.InputUrl });
        log.LogInformation("Input is a Url.");
    }
    else
    {
        jobInput = new JobInputAsset(assetName: data.InputAssetName);
        log.LogInformation($"Input is asset '{data.InputAssetName}'.");
    }

    Job job;
    try
    {
        // Job submission to Azure Media Services
        job = await SubmitJobAsync(
                                   client,
                                   log,
                                   config.ResourceGroup,
                                   config.AccountName,
                                   data.TransformName,
                                   jobName,
                                   jobInput,
                                   outputAssetName
                                   );
        log.LogInformation($"Job '{jobName}' submitted.");
    }
    catch (Exception e)
    {
        log.LogError("Error when submitting the job.");
        log.LogError($"{e.Message}");
        return ResponseBadRequest(req, e.Message);
    }

    AnswerBodyModel dataOk = new()
    {
        OutputAssetName = outputAsset.Name,
        JobName = job.Name
    };
    return ResponseOk(req, dataOk);
}

```

### [](https://learn.microsoft.com/en-us/azure/media-services/latest/integrate-azure-functions-dotnet-how-to#localsettingsjson)local.settings.json

Update the file with the following content (and replace the values).

JSONCopy

```
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "AadClientId": "00000000-0000-0000-0000-000000000000",
    "AadEndpoint": "https://login.microsoftonline.com",
    "AadSecret": "00000000-0000-0000-0000-000000000000",
    "AadTenantId": "00000000-0000-0000-0000-000000000000",
    "AccountName": "amsaccount",
    "ArmAadAudience": "https://management.core.windows.net/",
    "ArmEndpoint": "https://management.azure.com/",
    "ResourceGroup": "amsResourceGroup",
    "SubscriptionId": "00000000-0000-0000-0000-000000000000"
  }
}

```

[](https://learn.microsoft.com/en-us/azure/media-services/latest/integrate-azure-functions-dotnet-how-to#test-your-function)Test your function
----------------------------------------------------------------------------------------------------------------------------------------------

When you run the function locally in VS Code, the function should be exposed as:

urlCopy

```
http://localhost:7071/api/HttpTriggerEncode

```

To test it, you can use the REST client of your choice to do a POST on this URL using a JSON input body.

JSON input body example:

JSONCopy

```
{
    "inputUrl":"https://nimbuscdn-nimbuspm.streaming.mediaservices.windows.net/2b533311-b215-4409-80af-529c3e853622/Ignite-short.mp4",
    "transformName" : "TransformAS",
    "builtInPreset" :"AdaptiveStreaming"
 }

```

The function should return 200 OK with an output body containing the job and output asset names.
