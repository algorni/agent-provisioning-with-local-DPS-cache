// See https://aka.ms/new-console-template for more information
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



ILogger logger = null;

Console.WriteLine("Hello, IoT World!");

IConfiguration configuration = new ConfigurationBuilder()
  .AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true)
  //.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)  
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



logger.LogInformation("Initializing security using the local TPM...");
using SecurityProviderTpm security = new SecurityProviderTpmHsm(registrationId);


logger.LogInformation($"Initializing the device provisioning cache...");

IProvisioningDetailCache provisioningDetailCache = new ProvisioningDetailsFileStorage();

var provisioningDetails = provisioningDetailCache.GetProvisioningDetailResponseFromCache(registrationId);


if(provisioningDetails == null)
{
    logger.LogInformation($"Initializing the device provisioning client...");

    using var transport = new ProvisioningTransportHandlerAmqp();

    ProvisioningDeviceClient provClient = ProvisioningDeviceClient.Create(
        dpsEndpoint,
        dpsScopeId,
        security,
        transport);

    logger.LogInformation($"Initialized for registration Id {security.GetRegistrationID()}.");

    logger.LogInformation("Registering with the device provisioning service... ");

    //the method will attempt to retry in case of transient fault
    DeviceRegistrationResult result = await registerDevice(provClient);

    provisioningDetails = new ProvisioningResponse() { iotHubHostName = result.AssignedHub, deviceId = result.DeviceId };

    provisioningDetailCache.SetProvisioningDetailResponse(registrationId, provisioningDetails);
}


if (provisioningDetails != null)
{
    logger.LogInformation($"Device {provisioningDetails.deviceId} registered to {provisioningDetails.iotHubHostName}.");

    logger.LogInformation("Creating TPM authentication for IoT Hub...");
    IAuthenticationMethod auth = new DeviceAuthenticationWithTpm(provisioningDetails.deviceId, security);

    logger.LogInformation($"Testing the provisioned device with IoT Hub...");
    DeviceClient iotClient = DeviceClient.Create(provisioningDetails.iotHubHostName, auth, TransportType.Amqp);

    logger.LogInformation($"Registering the Method Call back for Reprovisioning...");
    await iotClient.SetMethodHandlerAsync("Reprovision",reprovisionDirectMethodCallback, iotClient);
    

    //now you should start a thred into this method and do your business while the Device client is still there connected. 
    await startBackgroundWork(iotClient);


    logger.LogInformation("Wait untile closed...");
       
    // Wait until the app unloads or is cancelled
    var cts = new CancellationTokenSource();
    AssemblyLoadContext.Default.Unloading += (ctx) => cts.Cancel();
    Console.CancelKeyPress += (sender, cpe) => cts.Cancel();

    await WhenCancelled(cts.Token);

    await iotClient.CloseAsync();
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




async Task startBackgroundWork(DeviceClient iotClient)
{
    //this is just an example...  sending a simple telemetry message...

    logger.LogInformation("Sending a telemetry message...");
    var message = new Message(Encoding.UTF8.GetBytes("TestMessage"));
    
    await iotClient.SendEventAsync(message);
}



//callback method for the Direct Method to force re-provisioning of the agent
async Task<MethodResponse> reprovisionDirectMethodCallback(MethodRequest methodRequest, object userContext)
{
    //check the method request
    



    MethodResponse methodResponse = new MethodResponse(200);

    return methodResponse;
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
