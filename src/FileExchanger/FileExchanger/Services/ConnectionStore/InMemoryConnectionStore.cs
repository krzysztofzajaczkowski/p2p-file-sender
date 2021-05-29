using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FileExchanger.Services.ConnectionStore
{
    public class InMemoryConnectionStore : IConnectionStore
    {
        public string HostId { get; set; }
        public void SetHostId(string hostId)
        {
            HostId = hostId;
        }
    }
}
