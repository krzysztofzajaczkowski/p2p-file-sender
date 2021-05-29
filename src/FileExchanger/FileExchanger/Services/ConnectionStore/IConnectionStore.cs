using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FileExchanger.Services.ConnectionStore
{
    public interface IConnectionStore
    {
        string HostId { get; }
        public void SetHostId(string hostId);
    }
}
