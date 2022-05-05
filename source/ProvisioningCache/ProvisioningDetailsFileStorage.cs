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


        public ProvisioningResponse GetProvisioningDetailResponseFromCache(string registrationId)
        {
            try
            {
                var provisioningResponseFile = File.ReadAllText(Path.Combine(dataDirectory, registrationId));

                ProvisioningResponse response = JsonConvert.DeserializeObject<ProvisioningResponse>(provisioningResponseFile);

                return response;
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        public void SetProvisioningDetailResponse(string registrationId, ProvisioningResponse provisioningDetails)
        {
            var provisioningDetailsJson = JsonConvert.SerializeObject(provisioningDetails);

            File.WriteAllText(Path.Combine(dataDirectory, registrationId), provisioningDetailsJson);
        }
    }
}