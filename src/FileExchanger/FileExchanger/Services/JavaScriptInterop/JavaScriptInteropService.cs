using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.AspNetCore.NodeServices;

namespace FileExchanger.Services.JavaScriptInterop
{
    public class JavaScriptInteropService
    {
        private readonly INodeServices _nodeServices;

        public JavaScriptInteropService(INodeServices nodeServices)
        {
            _nodeServices = nodeServices;
        }

        public async Task<List<byte>> Encrypt(List<byte> data, List<byte> key, List<byte> iv, CipherMode mode)
        {
            var base64 = await _nodeServices.InvokeAsync<string>("Encrypt.js", Convert.ToBase64String(data.ToArray()),
                Convert.ToBase64String(key.ToArray()), Convert.ToBase64String(iv.ToArray()), mode.ToString());
            var bytes = Convert.FromBase64String(base64).ToList();
            return bytes;
        }

        public async Task<List<byte>> Decrypt(List<byte> data, List<byte> key, List<byte> iv, CipherMode mode)
        {
            var base64 = await _nodeServices.InvokeAsync<string>("Decrypt.js", Convert.ToBase64String(data.ToArray()),
                Convert.ToBase64String(key.ToArray()), Convert.ToBase64String(iv.ToArray()), mode.ToString());
            var bytes = Convert.FromBase64String(base64).ToList();
            return bytes;
        }
    }
}
