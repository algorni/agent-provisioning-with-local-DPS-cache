// See https://aka.ms/new-console-template for more information
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Provisioning.Client;
using Microsoft.Azure.Devices.Provisioning.Client.Transport;
using Microsoft.Azure.Devices.Provisioning.Security;
using Microsoft.Azure.Devices.Shared;
using Microsoft.Extensions.Configuration;
using ProvisioningCache;
using System.Text;

Console.WriteLine("Hello, IoT World!");

Console.WriteLine("Sample code to leverage TPM chipset to onboard a device with DPS and connect to IoT Hub");

IConfiguration configuration = new ConfigurationBuilder()
  .AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true)
  .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)  
  .AddEnvironmentVariables()
  .AddCommandLine(args)
  .Build();

string dpsEndpoint = configuration["dpsEndpoint"];
string scopeId = configuration["dpsScopeId"];
string registrationId = configuration["registrationId"]; 


Console.WriteLine("Initializing security using the local TPM...");
using SecurityProviderTpm security = new SecurityProviderTpmHsm(registrationId);

Console.WriteLine($"Initializing the device provisioning cache...");

IProvisioningDetailCache provisioningDetailCache = new ProvisioningDetailsFileStorage();

var provisioningDetails = provisioningDetailCache.GetProvisioningDetailResponseFromCache(registrationId);


if ( provisioningDetails == null)
{
    Console.WriteLine($"Initializing the device provisioning client...");

    using var transport = new ProvisioningTransportHandlerAmqp();

    ProvisioningDeviceClient provClient = ProvisioningDeviceClient.Create(
        dpsEndpoint,
        scopeId,
        security,
        transport);

    Console.WriteLine($"Initialized for registration Id {security.GetRegistrationID()}.");

    Console.WriteLine("Registering with the device provisioning service... ");

    //add Polly for the Retry Policy
    //https://github.com/App-vNext/Polly
    DeviceRegistrationResult result = await provClient.RegisterAsync();

    Console.WriteLine($"Registration status: {result.Status}.");
    if (result.Status != ProvisioningRegistrationStatusType.Assigned)
    {
        Console.WriteLine($"Registration status did not assign a hub, so exiting this sample.");
        return;
    }

    provisioningDetails = new ProvisioningResponse() { iotHubHostName = result.AssignedHub, deviceId = result.DeviceId };

    provisioningDetailCache.SetProvisioningDetailResponse(registrationId, provisioningDetails);
}


if (provisioningDetails != null)
{
    Console.WriteLine($"Device {provisioningDetails.deviceId} registered to {provisioningDetails.iotHubHostName}.");

    Console.WriteLine("Creating TPM authentication for IoT Hub...");
    IAuthenticationMethod auth = new DeviceAuthenticationWithTpm(provisioningDetails.deviceId, security);

    Console.WriteLine($"Testing the provisioned device with IoT Hub...");
    using DeviceClient iotClient = DeviceClient.Create(provisioningDetails.iotHubHostName, auth, TransportType.Amqp);

    Console.WriteLine("Sending a telemetry message...");
    using var message = new Message(Encoding.UTF8.GetBytes("TestMessage"));
    await iotClient.SendEventAsync(message);

    await iotClient.CloseAsync();
    Console.WriteLine("Finished.");

    ///TODO: add infinite loop and register to teh Direct Method to wipe local provisioning details
}



