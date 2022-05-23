// See https://aka.ms/new-console-template for more information
using Agent.Common;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Logging;
using Microsoft.Azure.Devices.Provisioning.Client;
using Microsoft.Azure.Devices.Provisioning.Client.Transport;
using Microsoft.Azure.Devices.Provisioning.Security;
using Microsoft.Azure.Devices.Shared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ProvisioningCache;
using System.Runtime.Loader;
using System.Text;
using Tpm2Lib;


//the IoT Device Client object (when is not null it means we have an object initialized)
DeviceClient deviceClient = null;

ILogger logger = null;

Console.WriteLine("Hello, IoT World!");

IConfiguration configuration = new ConfigurationBuilder()
  .AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true)
  .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)  
  .AddEnvironmentVariables()
  .AddCommandLine(args)
  .Build();

string logLevel = configuration["logLevel"];

if (!string.IsNullOrEmpty(logLevel))
{
    if (Microsoft.Azure.Devices.Edge.Util.Logger.LogLevelDictionary.ContainsKey(logLevel))
    {
        Console.WriteLine($"Setting Log Level to {logLevel}");
        Microsoft.Azure.Devices.Edge.Util.Logger.SetLogLevel(logLevel);
    }
    else
    {
        Console.WriteLine($"Setting Log Level to info as {logLevel} is an unrecognized log level");
        Microsoft.Azure.Devices.Edge.Util.Logger.SetLogLevel("info");
    }
}
else
{
    Console.WriteLine("Set Log Level to info.");
    Microsoft.Azure.Devices.Edge.Util.Logger.SetLogLevel("info");
}

logger = Microsoft.Azure.Devices.Edge.Util.Logger.Factory.CreateLogger<Program>();

const string SdkEventProviderPrefix = "Microsoft-Azure-";
// Instantiating this seems to do all we need for outputting SDK events to our console log
_ = new ConsoleEventListener(SdkEventProviderPrefix, logger);


logger.LogInformation("Sample code to leverage TPM chipset to onboard a device with DPS and connect to IoT Hub");


string dpsEndpoint = configuration["dpsEndpoint"];
logger.LogInformation($"DPS Endpoint: {dpsEndpoint}");

string dpsScopeId = configuration["dpsScopeId"];
logger.LogInformation($"DPS ScopeId: {dpsScopeId}");

string registrationId = configuration["registrationId"];
logger.LogInformation($"DPS RegistrationId: {registrationId}");


// The Cancellation Token is used to quit from the application when the job is done...
var cts = new CancellationTokenSource();


//as attestation mechanism we are using TPM chip.
logger.LogInformation("Initializing security using the local TPM...");
SecurityProviderTpm security = new SecurityProviderTpmHsm(registrationId);



//the provisioning detail cache is a simple way to store the provisioning details locally into the agent host 
//instead of registering the device via Device Provisioning Service all the time!
logger.LogInformation($"Initializing the device provisioning cache...");

IProvisioningDetailCache provisioningDetailCache = new ProvisioningDetailsFileStorage();

//get the cached info (if available)
var provisioningDetails = await provisioningDetailCache.GetProvisioningDetailResponseFromCache(registrationId);


if(provisioningDetails == null)
{
    //cached info not avaiable, 1st time provisioning!!  In thi case create a DPS Device Client from the SDK
    //and perform the Registration of the device.
    //The registration will complete with the Provisioning Details which basically are the IoT Hub hostname to which the
    //IoT Hub Device client will connect to.
    //The registration process will follow the steps defined into the Enrollment in DPS

    await getProvisioningDetailsFromDPS();  
}



if (provisioningDetails != null)
{  
    //this is going to create a device client, suscribe to the Reprovisioning Callback and start the background job to run the business of this agent! 
    await startAsyncBackgroundWork();

    logger.LogInformation("Wait untile closed...");
       
    AssemblyLoadContext.Default.Unloading += (ctx) => cts.Cancel();
    Console.CancelKeyPress += (sender, cpe) => cts.Cancel();

    await WhenCancelled(cts.Token);
        
    Console.WriteLine("Finished.");
}



/// <summary>
/// Handles cleanup operations when app is cancelled or unloads
/// </summary>
Task WhenCancelled(CancellationToken cancellationToken)
{
    var tcs = new TaskCompletionSource<bool>();
    cancellationToken.Register(s => ((TaskCompletionSource<bool>)s).SetResult(true), tcs);
    return tcs.Task;
}








async Task getProvisioningDetailsFromDPS()
{
    logger.LogInformation($"Getting Provisioning information from DPS. Initializing the device provisioning client...");

    using (var transport = new ProvisioningTransportHandlerAmqp())
    {
        ProvisioningDeviceClient provClient = ProvisioningDeviceClient.Create(
            dpsEndpoint,
            dpsScopeId,
            security,
            transport);

        logger.LogInformation($"Initialized for registration Id {security.GetRegistrationID()}.");

        logger.LogInformation("Registering with the device provisioning service... ");

        //the method will attempt to retry in case of transient fault
        DeviceRegistrationResult result = await registerDevice(provClient);

        if (result != null)
        {
            //store the provisioning details into the local cache for the subsequential start of the agent to avoid reprovisioning all the time
            provisioningDetails = new ProvisioningResponse() { iotHubHostName = result.AssignedHub, deviceId = result.DeviceId };

            await provisioningDetailCache.SetProvisioningDetailResponse(registrationId, provisioningDetails);
        }
    }
}


///Ok just get the device client and start to do the job
async Task startAsyncBackgroundWork()
{
    deviceClient = await getDeviceClientAndRegisterReprovisionCallback();

    //this is just a simplex example... you should just start another thred and return ASAP the control to this method as it could block the Direct Method call for the re-provisioning!

    logger.LogInformation("Sending a telemetry message...");
    var message = new Message(Encoding.UTF8.GetBytes("TestMessage"));

    await deviceClient.SendEventAsync(message);

    logger.LogInformation("Job done, but just wait for a possible reprovisioning call!");

    //job done... trigger the completion of the waiting task in the main program if you want
    //in this case not doing that so the main program will stay listening in background 
    //await deviceClient.CloseAsync();
    //cts.Cancel();
}


async Task<DeviceClient> getDeviceClientAndRegisterReprovisionCallback()
{
    logger.LogInformation($"Starte the process to create a client for DeviceId: {provisioningDetails.deviceId} registered to: {provisioningDetails.iotHubHostName}.");

    logger.LogInformation("Creating TPM authentication for IoT Hub...");
    IAuthenticationMethod auth = new DeviceAuthenticationWithTpm(provisioningDetails.deviceId, security);

    logger.LogInformation($"Creating the Device Client to connect to IoT Hub...");
    DeviceClient deviceClient = DeviceClient.Create(provisioningDetails.iotHubHostName, auth, TransportType.Amqp);

    logger.LogInformation($"Registering the Method Call back for Reprovisioning...");
    await deviceClient.SetMethodHandlerAsync(ReprovisioningCommand.DirectMethodName, reprovisionDirectMethodCallback, null);

    return deviceClient;
}


//callback method for the Direct Method to force re-provisioning of the agent
async Task<MethodResponse> reprovisionDirectMethodCallback(MethodRequest methodRequest, object userContext)
{
    //parse the method request
    ReprovisioningCommandRequest reprovisioningCommandRequest = ReprovisioningCommandRequest.ParseJSON(methodRequest.DataAsJson);

    MethodResponse methodResponse;
    ReprovisioningCommandResponse reprovisioningCommandResponse = new ReprovisioningCommandResponse();


    if (reprovisioningCommandRequest != null)
    {  
        try
        {
            await provisioningDetailCache.ClearProvisioningDetail(registrationId);

            reprovisioningCommandResponse.ReprovisionStatus = ReprovisionResultEnum.Success;
            reprovisioningCommandResponse.ReprovisionResult = "Ok Job Done";

            methodResponse = new MethodResponse(reprovisioningCommandResponse.ToJSONBytes(), 200);

            await recreateDeviceClientAndRestartDoingJob();
        }
        catch (ClearProvisioningDetalException ex)
        {
            reprovisioningCommandResponse.ReprovisionStatus = ReprovisionResultEnum.ErrorWhileWipingProvisioningDetails;
            reprovisioningCommandResponse.ReprovisionResult = ex.Message;

            methodResponse = new MethodResponse(reprovisioningCommandResponse.ToJSONBytes(), 500);
        }                
    }
    else
    {
        reprovisioningCommandResponse.ReprovisionStatus = ReprovisionResultEnum.RequestParsingErrors;

        methodResponse = new MethodResponse(reprovisioningCommandResponse.ToJSONBytes(), 500);
    }

    return methodResponse;
}

async Task recreateDeviceClientAndRestartDoingJob()
{
    if (deviceClient != null)
        await deviceClient.CloseAsync();

    await getProvisioningDetailsFromDPS();

    //this is going to create a device client, suscribe to the Reprovisioning Callback and start the background job to run the business of this agent! 
    await startAsyncBackgroundWork();
}


//retriable register device method
async Task<DeviceRegistrationResult> registerDevice(ProvisioningDeviceClient provisioningDeviceClient, int currentAttempt = 0, int maxAttempt = 10)
{
    logger.LogDebug($"registerDevice -> Current Attempt: {currentAttempt} Max Attempt: {maxAttempt}");

    DeviceRegistrationResult result = null;

    try
    {
        logger.LogInformation($"Attempt {currentAttempt}");

        result = await provisioningDeviceClient.RegisterAsync();

        logger.LogInformation($"Registration status: {result.Status}.");

        if (result.Status != ProvisioningRegistrationStatusType.Assigned)
        {
            logger.LogError($"Registration status did not assign a hub, so exiting this sample.");
            return null;
        }   
    }
    catch (Microsoft.Azure.Devices.Provisioning.Client.ProvisioningTransportException ex) when (ex.IsTransient) //&& ex.ErrorDetails.ErrorCode == (int)System.Net.HttpStatusCode.TooManyRequests)
    {
        logger.LogWarning($"\tTransient error trying to register the device. {ex.Message}.");
                
        if (currentAttempt < maxAttempt)
        {
            TimeSpan delay = generateDelayWithJitterForRetry(currentAttempt);

            logger.LogInformation($"\tOperation will retry after {delay.TotalSeconds} seconds.");

            await Task.Delay(delay);

            return await registerDevice(provisioningDeviceClient, ++currentAttempt, maxAttempt).ConfigureAwait(false);
        }
        else
        {
            return null;
        }       
    }
    catch (Exception ex)
    {
        Console.WriteLine($"\tAn unrecoverable error happened trying  to register the device.\nError {ex.Message}");
        return null;
    }
    
    return result;
}


TimeSpan generateDelayWithJitterForRetry(int attemptNumber)
{
    const int jitterMax = 5;
    const int jitterMin = 0;

    TimeSpan exponentialDelay = TimeSpan.FromSeconds( Math.Pow(1.85, (double)attemptNumber) );   
    
    var random = new Random();
    double jitterSeconds = random.NextDouble() * jitterMax + jitterMin;

    TimeSpan delayWithJitter = exponentialDelay.Add(TimeSpan.FromSeconds(jitterSeconds));

    return delayWithJitter;
}