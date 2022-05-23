using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProvisioningCache
{
    public class ClearProvisioningDetalException : ApplicationException
    {

        public ClearProvisioningDetalException(string? message) : base(message)
        {
            
        }

        public ClearProvisioningDetalException(string? message, Exception? innerException) : base(message, innerException)
        {

        }
    }
}
