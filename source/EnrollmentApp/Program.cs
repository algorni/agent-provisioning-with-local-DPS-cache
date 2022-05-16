// See https://aka.ms/new-console-template for more information
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;
using Microsoft.Azure.Devices.Provisioning.Security;
using Microsoft.Azure.Devices.Provisioning.Service;
using Microsoft.Azure.Devices.Shared;
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

//https://docs.microsoft.com/en-us/azure/iot-dps/concepts-control-access-dps-azure-ad

TokenCredential tokenCredential = null;

// DefaultAzureCredential supports different authentication mechanisms and determines the appropriate credential type based of the environment it is executing in.
// It attempts to use multiple credential types in an order until it finds a working credential.
// For more info see https://docs.microsoft.com/en-us/dotnet/api/azure.identity?view=azure-dotnet.

//tokenCredential = new DefaultAzureCredential();

//DeviceCodeCredential leverage teh Code Flow authnetication, in this case the user need to use another device browser to authenticate 

tokenCredential = new DeviceCodeCredential(new DeviceCodeCredentialOptions()
{
    DeviceCodeCallback = deviceCodeCallback,
    //add the application ID for better customization      
});


//now connecting to the DPS and create the indivisual enrollment
using (ProvisioningServiceClient provisioningServiceClient =
          // ProvisioningServiceClient.CreateFromConnectionString(dpsConnectionString))
          ProvisioningServiceClient.Create(dpsEndpoint, tokenCredential))
{
    Console.WriteLine("Creating or update an individualEnrollment...");
    Attestation attestation = new TpmAttestation(base64EndorsmentKey);

    IndividualEnrollment individualEnrollment;

    //first of all try to load if exist already to update it
    individualEnrollment = await getExistingEnrollmentOrCreatwNewOne(provisioningServiceClient,registrationId, deviceId, attestation);

    if (individualEnrollment != null)
    {
        Console.WriteLine("Adding new individualEnrollment...");
        IndividualEnrollment individualEnrollmentResult = await createOrUpdateEnrollment(provisioningServiceClient, individualEnrollment);

        if ( individualEnrollment != null)
        {
            Console.WriteLine("\nRegistration succeded.");
            Console.WriteLine(individualEnrollmentResult);
        }
        else
        {
            Console.WriteLine("\nRegistration failed.");
        }
    }
    else
    {
        Console.WriteLine("\nRegistration failed.");
    }
}


async Task<IndividualEnrollment> getExistingEnrollmentOrCreatwNewOne(ProvisioningServiceClient provisioningServiceClient, string registrationId, string deviceId, Attestation attestation, int currentAttempt = 0, int maxAttempt = 10)
{
    Console.WriteLine($"Looking if a registraiton {registrationId} exist already...");

    IndividualEnrollment individualEnrollment = null;

    try
    {
        individualEnrollment = await provisioningServiceClient.GetIndividualEnrollmentAsync(registrationId).ConfigureAwait(false);

        Console.WriteLine($"Registraiton {registrationId} already exist. Going to update it.");
    }
    catch (ProvisioningServiceClientHttpException ex) when (!ex.IsTransient && ex.StatusCode == System.Net.HttpStatusCode.NotFound) 
    {
        Console.WriteLine($"Registraiton {registrationId} was not existing. Going to create it.");
                
        individualEnrollment = new IndividualEnrollment(registrationId, attestation);

        individualEnrollment.ReprovisionPolicy = new ReprovisionPolicy() { UpdateHubAssignment = true, MigrateDeviceData = true };

        var tags = new TwinCollection();
        tags["initialProvisionedDate"] = DateTime.UtcNow.ToString();

        var desiredProperty = new TwinCollection();
        individualEnrollment.InitialTwinState = new TwinState(tags, desiredProperty);
    }
    catch (ProvisioningServiceClientHttpException ex) when (ex.IsTransient && ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
    {
        Console.WriteLine($"\tTransient error trying getting existin registration {registrationId}. {ex.ErrorMessage}.");
        if (ex.Fields.Keys.Contains("Retry-After") && int.TryParse(ex.Fields["Retry-After"], out var delaySeconds))
        {
            if (currentAttempt < maxAttempt)
            {
                Console.WriteLine($"\tOperation will retry after {delaySeconds} seconds.");

                await Task.Delay(TimeSpan.FromSeconds(delaySeconds));

                return await getExistingEnrollmentOrCreatwNewOne(provisioningServiceClient,registrationId,deviceId,attestation,++currentAttempt,maxAttempt).ConfigureAwait(false);
            }
            else
            {
                return null;
            }            
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"\tAn unrecoverable error happened trying getting existin registration {registrationId}. Error {ex.Message}");
        return null;
    }
    
    individualEnrollment.DeviceId = deviceId;
    
    return individualEnrollment;
}


async Task<IndividualEnrollment> createOrUpdateEnrollment(ProvisioningServiceClient provisioningServiceClient, IndividualEnrollment individualEnrollment, int currentAttempt = 0, int maxAttempt = 10)
{
    Console.WriteLine($"Creating or Updating the registraiton {individualEnrollment.RegistrationId}...");

    IndividualEnrollment individualEnrollmentCreated = null;

    try
    {
        individualEnrollmentCreated = await provisioningServiceClient.CreateOrUpdateIndividualEnrollmentAsync(individualEnrollment).ConfigureAwait(false);

        Console.WriteLine($"Registraiton {individualEnrollment.RegistrationId} done!");
    }    
    catch (ProvisioningServiceClientHttpException ex) when (ex.IsTransient && ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
    {
        Console.WriteLine($"\tTransient error trying creating or updating registration {individualEnrollment.RegistrationId}. {ex.ErrorMessage}.");
        if (ex.Fields.Keys.Contains("Retry-After") && int.TryParse(ex.Fields["Retry-After"], out var delaySeconds))
        {
            if (currentAttempt < maxAttempt)
            {
                Console.WriteLine($"\tOperation will retry after {delaySeconds} seconds.");

                await Task.Delay(TimeSpan.FromSeconds(delaySeconds));

                return await createOrUpdateEnrollment(provisioningServiceClient, individualEnrollment, ++currentAttempt, maxAttempt).ConfigureAwait(false);
            }
            else
            {
                return null;
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"\tAn unrecoverable error happened trying  creating or updating registration {registrationId}. Error {ex.Message}");
        return null;
    }

    individualEnrollmentCreated.DeviceId = deviceId;

    return individualEnrollmentCreated;
}



//this is the callback for teh Device Code Flow, it will show a QR Code with the Login link and instruction to follow to authenticate with AAD credential into another device
async Task deviceCodeCallback(DeviceCodeInfo deviceCodeInfo, CancellationToken cancellationToken)
{
    string qrCodeText = deviceCodeInfo.VerificationUri.ToString();

    QRCodeGenerator qrGenerator = new QRCodeGenerator();

    //PayloadGenerator.Url url = new PayloadGenerator.Url(qrCodeText);

    QRCodeData qrCodeData = qrGenerator.CreateQrCode(qrCodeText, QRCodeGenerator.ECCLevel.Q, forceUtf8:true); 
    
    AsciiQRCode qrCode = new AsciiQRCode(qrCodeData);    
    string qrCodeAsAsciiArt = qrCode.GetGraphic(1);

    Console.WriteLine($"\n\nPlease use your phone to scan the following QR Code or visit this URL: {deviceCodeInfo.VerificationUri}\n Then enter the following code: {deviceCodeInfo.UserCode} and login with your AAD credential to allow access to DPS\n\n");

    Console.WriteLine(qrCodeAsAsciiArt);      
}