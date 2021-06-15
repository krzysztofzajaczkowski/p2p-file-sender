using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace FileExchanger.Requests
{
    public class PackageTextRequest
    {
        public CipherMode Mode { get; set; } = CipherMode.CBC;
        public string Text { get; set; }
    }
}
