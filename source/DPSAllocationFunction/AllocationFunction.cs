using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.Azure.Devices.Provisioning.Service;
using Microsoft.Azure.Devices.Shared;
using ProvisioningCache;

namespace DPSAllocationFunction
{
    public static class AllocationFunction
    {
        [FunctionName("Allocation")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("DPS Allocation Function triggered.");

            // Get request body
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            
            log.LogInformation($"Request.Body:\n{requestBody}");
            
            // Get registration ID of the device
            string regId = data?.deviceRuntimeContext?.registrationId;

            CustomAllocationPolicyResponse provisioningResponse = new CustomAllocationPolicyResponse();

            // Specify the initial tags for the device including the IoT Hub to which they need to head to....
            TwinCollection tags = new TwinCollection();
            tags["deviceType"] = "heatpump-abc";
                       
            // Specify the initial desired properties for the device.
            TwinCollection properties = new TwinCollection();
            properties["state"] = "on";
            properties["temperatureSetting"] = "65";

            // Add the initial twin state to the response.
            TwinState twinState = new TwinState(tags, properties);

            provisioningResponse.initialTwin = twinState;
            provisioningResponse.iotHubHostName = "IoT-Hub-FTA-Scenario.azure-devices.net";
                        
            log.LogInformation($"Allocation Response:\n{JsonConvert.SerializeObject(provisioningResponse)}");

            return new OkObjectResult(provisioningResponse);
        }
    }
}
