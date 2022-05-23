using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Agent.Common
{   public class ReprovisioningCommandResponse
    {
        /// <summary>
        /// Reprovision Status
        /// </summary>
        public ReprovisionResultEnum ReprovisionStatus { get; set; }

        /// <summary>
        /// Message explainnig the Reprovisionining Status
        /// </summary>
        public string ReprovisionResult { get; set; }


        /// <summary>
        /// Get JSON Byte Array
        /// </summary>
        /// <returns></returns>
        public byte[] ToJSONBytes()
        {
            var jsonString = JsonSerializer.Serialize(this);

            byte[] byteArray = Encoding.UTF8.GetBytes(jsonString);

            return byteArray;
        }
    }
}
