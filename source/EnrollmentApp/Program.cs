// See https://aka.ms/new-console-template for more information
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;
using Microsoft.Azure.Devices.Provisioning.Security;
using Microsoft.Azure.Devices.Provisioning.Service;
using Microsoft.Extensions.Configuration;

Console.WriteLine("Hello, IoT World!");

Console.WriteLine("Sample code to leverage TPM chipset to Enroll an agent into DPS");

IConfiguration configuration = new ConfigurationBuilder()  
  .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
  .AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true)
  .AddEnvironmentVariables()
  .AddCommandLine(args)
  .Build();

string dpsEndpoint = configuration["dpsEndpoint"];
string registrationId = configuration["registrationId"];
string deviceId = configuration["deviceId"];

//getting the Endorsment key from TPM
using var security = new SecurityProviderTpmHsm(null);

var endorsmentKey = security.GetEndorsementKey();
var base64EndorsmentKey = Convert.ToBase64String(endorsmentKey);

Console.WriteLine($"Your EK is {base64EndorsmentKey}");

ProvisioningStatus provisioningStatus = ProvisioningStatus.Enabled;


//https://docs.microsoft.com/en-us/azure/iot-dps/concepts-control-access-dps-azure-ad

// DefaultAzureCredential supports different authentication mechanisms and determines the appropriate credential type based of the environment it is executing in.
// It attempts to use multiple credential types in an order until it finds a working credential.
// For more info see https://docs.microsoft.com/en-us/dotnet/api/azure.identity?view=azure-dotnet.
TokenCredential tokenCredential = new DefaultAzureCredential();

//authentication can be done also with
//https://docs.microsoft.com/en-us/azure/active-directory/develop/v2-oauth2-device-code
//https://docs.microsoft.com/en-us/azure/active-directory/develop/scenario-desktop-acquire-token-device-code-flow?tabs=dotnet
//https://github.com/azure-samples/active-directory-dotnetcore-devicecodeflow-v2

//now connecting to the DPS and create the indivisual enrollment
using (ProvisioningServiceClient provisioningServiceClient =
          // ProvisioningServiceClient.CreateFromConnectionString(dpsConnectionString))
          ProvisioningServiceClient.Create(dpsEndpoint, tokenCredential))
{
    Console.WriteLine("Creating a new individualEnrollment...");
    Attestation attestation = new TpmAttestation(base64EndorsmentKey);

    IndividualEnrollment individualEnrollment = new IndividualEnrollment(registrationId, attestation);

    // The following parameters are optional. Remove them if you don't need them.
    individualEnrollment.DeviceId = deviceId;
    individualEnrollment.ProvisioningStatus = provisioningStatus;

    Console.WriteLine("Adding new individualEnrollment...");
    IndividualEnrollment individualEnrollmentResult = await provisioningServiceClient.CreateOrUpdateIndividualEnrollmentAsync(individualEnrollment).ConfigureAwait(false);

    Console.WriteLine("\nIndividualEnrollment created with success.");
    Console.WriteLine(individualEnrollmentResult);
}