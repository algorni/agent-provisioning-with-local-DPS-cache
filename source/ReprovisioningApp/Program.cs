// See https://aka.ms/new-console-template for more information
using Agent.Common;
using Azure.Core;
using Azure.Identity;
using Microsoft.Azure.Devices;
using Microsoft.Extensions.Configuration;

Console.WriteLine("Hello, IoT World!");

Console.WriteLine("\nSample app to send a Reprovisioning Command to an agent!");

IConfiguration configuration = new ConfigurationBuilder()
  .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
  .AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true)
  .AddEnvironmentVariables()
  .AddCommandLine(args)
  .Build();

string iotHubHostName = configuration["iotHubHostName"];
string deviceId = configuration["deviceId"];


//use Default Azure Credential to auth to AAD 
TokenCredential tokenCredential = new DefaultAzureCredential();

Console.WriteLine("Creating an IoT Hub service client using DefaultAzureCredential"); 

var serviceClient = ServiceClient.Create(iotHubHostName, tokenCredential, TransportType.Amqp_WebSocket_Only);

Console.WriteLine("Performin direct method call to force Reprovisioning of the device");

CloudToDeviceMethod cloudToDeviceMethod = new CloudToDeviceMethod(ReprovisioningCommand.DirectMethodName, TimeSpan.FromSeconds(90), TimeSpan.FromSeconds(60));

ReprovisioningCommandRequest reprovisioningCommandRequest = new ReprovisioningCommandRequest() { ReprovisionReason = ReprovisioningReasonEnum.ChangeInConfigRequired };

cloudToDeviceMethod.SetPayloadJson(reprovisioningCommandRequest.ToJSON());

try
{
    var result = await serviceClient.InvokeDeviceMethodAsync(deviceId, cloudToDeviceMethod);

    Console.WriteLine("Direct Method called");

    Console.WriteLine(result.GetPayloadAsJson());
}
catch ( Exception ex)
{
    Console.WriteLine($"An error happened while calling the remote direct method:\n{ex.ToString()}");
}

