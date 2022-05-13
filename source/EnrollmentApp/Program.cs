// See https://aka.ms/new-console-template for more information
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;
using Microsoft.Azure.Devices.Provisioning.Security;
using Microsoft.Azure.Devices.Provisioning.Service;
using Microsoft.Extensions.Configuration;
using QRCoder;

Console.WriteLine("Hello, IoT World!");

Console.WriteLine("\nSample code to leverage TPM chipset to Enroll an agent into DPS");

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

Console.WriteLine($"\nYour EK is {base64EndorsmentKey}\n");

ProvisioningStatus provisioningStatus = ProvisioningStatus.Enabled;


//https://docs.microsoft.com/en-us/azure/iot-dps/concepts-control-access-dps-azure-ad

TokenCredential tokenCredential = null;

// DefaultAzureCredential supports different authentication mechanisms and determines the appropriate credential type based of the environment it is executing in.
// It attempts to use multiple credential types in an order until it finds a working credential.
// For more info see https://docs.microsoft.com/en-us/dotnet/api/azure.identity?view=azure-dotnet.

//tokenCredential =  = new DefaultAzureCredential();

//DeviceCodeCredential leverage teh Code Flow authnetication, in this case the user need to use another device browser to authenticate 

tokenCredential = new DeviceCodeCredential(new DeviceCodeCredentialOptions() 
    { 
        DeviceCodeCallback = deviceCodeCallback 
        //add the application ID for better customization
    });


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


static async Task deviceCodeCallback(DeviceCodeInfo deviceCodeInfo, CancellationToken cancellationToken)
{
    string qrCodeText = deviceCodeInfo.VerificationUri.ToString();

    QRCodeGenerator qrGenerator = new QRCodeGenerator();

    PayloadGenerator.Url url = new PayloadGenerator.Url(qrCodeText);

    QRCodeData qrCodeData = qrGenerator.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);
    AsciiQRCode qrCode = new AsciiQRCode(qrCodeData);
    string qrCodeAsAsciiArt = qrCode.GetGraphic(1);

    Console.WriteLine($"\n\nPlease use your phone to scan the following QR Code and enter the following code: {deviceCodeInfo.UserCode} then login with your AAD credential to allow access to DPS\n\n");

    Console.WriteLine(qrCodeAsAsciiArt);      
}