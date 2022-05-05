using Microsoft.Azure.Devices.Provisioning.Service;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProvisioningCache
{
    public abstract class ProvisioningDetailsBase
    {
        public string iotHubHostName { get; set; }        
    }
}
