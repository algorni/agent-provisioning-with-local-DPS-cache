using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ProvisioningCache
{
    public class ProvisioningDetailsFileStorage : IProvisioningDetailCache
    {
        private string dataDirectory = null;

        /// <summary>
        /// ctor
        /// </summary>
        public ProvisioningDetailsFileStorage()
        {
            dataDirectory = Environment.GetEnvironmentVariable("ProvisioningDetailsDataDirectory");
        }

        public async Task<ProvisioningResponse> GetProvisioningDetailResponseFromCache(string registrationId)
        {
            try
            {
                var filePath = Path.Combine(dataDirectory, registrationId);

                var provisioningResponseFile = await File.ReadAllTextAsync(filePath);

                ProvisioningResponse response = JsonConvert.DeserializeObject<ProvisioningResponse>(provisioningResponseFile);

                return response;
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        public async Task SetProvisioningDetailResponse(string registrationId, ProvisioningResponse provisioningDetails)
        {
            var provisioningDetailsJson = JsonConvert.SerializeObject(provisioningDetails);

            File.WriteAllText(Path.Combine(dataDirectory, registrationId), provisioningDetailsJson);
        }

        public async Task ClearProvisioningDetail(string registrationId)
        {
            var filePath = Path.Combine(dataDirectory, registrationId);

            var fileExist = File.Exists(filePath);

            if ( !fileExist)
            {
                throw new ClearProvisioningDetalException($"Provisioning details file {filePath} not present for {registrationId}");
            }
            else
            {
                try
                {
                    File.Delete(filePath);
                }
                catch (Exception ex)
                {
                    throw new ClearProvisioningDetalException($"An error occurred while deleting provisioning detail file: {filePath}", ex);
                }
            }            
        }
    }
}