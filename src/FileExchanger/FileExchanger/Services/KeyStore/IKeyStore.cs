using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace FileExchanger.Services.KeyStore
{
    public interface IKeyStore
    {
        void SaveEncryptedKeys(string publicKey, string privateKey, string hashedPassword);
        (string PublicKey, string PrivateKey) GetEncryptedKeys(string hashedPassword);
        bool KeysExists();
        void DeleteKeys();
    }
}
