using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace FileExchanger.Services.KeyStore
{
    public class InMemoryKeyStore : IKeyStore
    {
        private string _privateKey = string.Empty;
        private string _publicKey = string.Empty;
        private string _hashedPassword = string.Empty;

        public void SaveEncryptedKeys(string publicKey, string privateKey, string hashedPassword)
        {
            _privateKey = privateKey;
            _publicKey = publicKey;
            _hashedPassword = hashedPassword;
        }

        public void DeleteKeys()
        {
            _publicKey = string.Empty;
            _privateKey = string.Empty;
        }

        public (string PublicKey, string PrivateKey) GetEncryptedKeys(string hashedPassword)
        {
            return _hashedPassword == hashedPassword ?
                (_publicKey, _privateKey) :
                (string.Empty, string.Empty);
        }

        public bool KeysExists()
        {
            return _privateKey != string.Empty && _publicKey != string.Empty;
        }
    }
}
