using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace FileExchanger.Services.KeyStore
{
    public class DirectoryKeyStore : IKeyStore
    {
        private readonly OSPlatform _platform;
        private readonly string _dirSeparator;
        private readonly string _publicKeyDir;
        private readonly string _privateKeyDir;
        private readonly string _hashedPasswordDir;
        private string PublicKeyPath => $"{_publicKeyDir}{_dirSeparator}public";
        private string PrivateKeyPath => $"{_privateKeyDir}{_dirSeparator}private";
        private string HashedPasswordPath => $"{_hashedPasswordDir}{_dirSeparator}pass";

        public DirectoryKeyStore()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                _platform = OSPlatform.Linux;
                _dirSeparator = "/";
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _platform = OSPlatform.Windows;
                _dirSeparator = @"\";
            }
            _publicKeyDir = @$"{Environment.CurrentDirectory}{_dirSeparator}keys{_dirSeparator}pub";
            _privateKeyDir = @$"{Environment.CurrentDirectory}{_dirSeparator}keys{_dirSeparator}priv";
            _hashedPasswordDir = $@"{Environment.CurrentDirectory}{_dirSeparator}hash";

            Directory.CreateDirectory(_publicKeyDir);
            Directory.CreateDirectory(_privateKeyDir);
        }

        public DirectoryKeyStore(string clientName)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                _platform = OSPlatform.Linux;
                _dirSeparator = "/";
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _platform = OSPlatform.Windows;
                _dirSeparator = @"\";
            }
            _publicKeyDir = @$"{Environment.CurrentDirectory}{_dirSeparator}keys{_dirSeparator}{clientName}{_dirSeparator}pub";
            _privateKeyDir = @$"{Environment.CurrentDirectory}{_dirSeparator}keys{_dirSeparator}{clientName}{_dirSeparator}priv";
            _hashedPasswordDir = $@"{Environment.CurrentDirectory}{_dirSeparator}{clientName}{_dirSeparator}hash";

            Directory.CreateDirectory(_publicKeyDir);
            Directory.CreateDirectory(_privateKeyDir);
        }

        public void DeleteKeys()
        {
            if (File.Exists(PublicKeyPath))
            {
                File.Delete(PublicKeyPath);
            }
            if (File.Exists(PrivateKeyPath))
            {
                File.Delete(PrivateKeyPath);
            }
        }

        public void SaveEncryptedKeys(string publicKey, string privateKey, string hashedPassword)
        {
            File.WriteAllText(PublicKeyPath, publicKey);
            File.WriteAllText(PrivateKeyPath, privateKey);
        }

        public (string PublicKey, string PrivateKey) GetEncryptedKeys(string hashedPassword)
        {
            var publicKey = string.Empty;
            var privateKey = string.Empty;
            if (File.Exists(PublicKeyPath))
            {
                publicKey = File.ReadAllText(PublicKeyPath);
            }

            if (File.Exists(PrivateKeyPath))
            {
                privateKey = File.ReadAllText(PrivateKeyPath);
            }

            return (publicKey, privateKey);
        }

        public bool KeysExists()
        {
            var a = File.Exists(PublicKeyPath) &&
                    File.Exists(PrivateKeyPath);
            return a;

        }
    }
}
