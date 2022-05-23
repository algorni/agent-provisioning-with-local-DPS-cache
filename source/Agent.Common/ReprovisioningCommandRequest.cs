using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Agent.Common
{
    public class ReprovisioningCommandRequest
    {
        /// <summary>
        /// Reason why device need to reprovision
        /// </summary>
        public ReprovisioningReasonEnum ReprovisionReason { get; set; }

        /// <summary>
        /// JSON
        /// </summary>
        /// <returns></returns>
        public string ToJSON()
        {
            //{"ReprovisionReason":0}

            string jsonString = JsonSerializer.Serialize(this);

            return jsonString;
        }

        public static ReprovisioningCommandRequest ParseJSON(string dataAsJson)
        {
            return JsonSerializer.Deserialize<ReprovisioningCommandRequest>(dataAsJson);
        }
    }
}
