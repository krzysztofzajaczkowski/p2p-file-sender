using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace FileExchanger.Requests
{
    public class PackageFileRequest
    {
        public CipherMode Mode { get; set; } = CipherMode.CBC;
    }
}
