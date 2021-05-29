using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using FileExchanger.Helpers;
using FileExchanger.Models;
using FileExchanger.Services.DummyData;
using FileExchanger.Services.JavaScriptInterop;
using FileExchanger.Services.KeyStore;

namespace FileExchanger.Services.Encryptor
{
    public class EncryptorService : IEncryptorService
    {
        private readonly IKeyStore _keyStore;
        private readonly IDummyDataService _dummyDataService;
        private readonly JavaScriptInteropService _javaScriptInteropService;
        private List<byte> _sessionKey;
        private List<Guid> _packageIds = new List<Guid>();
        private List<Package> _packages = new List<Package>();
        private byte[] _publicKey;
        private List<byte> _privateKey;
        private List<byte> _receiverPublicKey;
        private CipherMode _currentDataCipherMode;
        private int _currentDataBlockSize;

        public EncryptorService(IKeyStore keyStore, IDummyDataService dummyDataService, JavaScriptInteropService javaScriptInteropService)
        {
            _keyStore = keyStore;
            _dummyDataService = dummyDataService;
            _javaScriptInteropService = javaScriptInteropService;
        }

        public byte[] EncryptWithRsa(List<byte> data, List<byte> publicKey)
        {
            using var rsa = RSA.Create(512);
            rsa.ImportRSAPublicKey(publicKey.ToArray(), out _);
            return rsa.Encrypt(data.ToArray(), RSAEncryptionPadding.Pkcs1);
        }

        public byte[] DecryptSessionKeyWithRsa(string data)
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(data));
            if (decoded == _dummyDataService.KeyDummyData)
            {
                _dummyDataService.SetReceiveDummyDataOnly();
                return new byte[] { };
            }
            var key = Convert.FromBase64String(data).ToList();
            var decryptedSessionKey = DecryptWithRsa(key, _privateKey);
            return decryptedSessionKey;
        }

        public byte[] DecryptWithRsa(List<byte> data, List<byte> privateKey)
        {
            using var rsa = RSA.Create(512);
            rsa.ImportRSAPrivateKey(privateKey.ToArray(), out _);
            return rsa.Decrypt(data.ToArray(), RSAEncryptionPadding.Pkcs1);
        }

        public string EncryptDataToBase64(string text, byte[] symmetricEncryptionKey, CipherMode mode = CipherMode.CBC, int blockSize = 128)
        {
            using (var aesAlg = Aes.Create())
            {
                aesAlg.Mode = mode;
                aesAlg.BlockSize = blockSize;
                aesAlg.Padding = PaddingMode.PKCS7;
                using (var encryptor = aesAlg.CreateEncryptor(symmetricEncryptionKey, aesAlg.IV))
                {
                    using (var msEncrypt = new MemoryStream())
                    {
                        using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                        using (var swEncrypt = new StreamWriter(csEncrypt))
                        {
                            swEncrypt.Write(text);
                        }

                        var iv = aesAlg.IV;

                        var decryptedContent = msEncrypt.ToArray();

                        var result = new byte[iv.Length + decryptedContent.Length];

                        Buffer.BlockCopy(iv, 0, result, 0, iv.Length);
                        Buffer.BlockCopy(decryptedContent, 0, result, iv.Length, decryptedContent.Length);

                        return Convert.ToBase64String(result);
                    }
                }
            }
        }

        public string DecryptDataFromBase64(string combinedKey, byte[] symmetricEncryptionKey, CipherMode mode = CipherMode.CBC, int blockSize = 128)
        {
            var fullCipher = Convert.FromBase64String(combinedKey);

            var iv = new byte[16];
            var cipher = new byte[fullCipher.Length - 16];

            Buffer.BlockCopy(fullCipher, 0, iv, 0, iv.Length);
            Buffer.BlockCopy(fullCipher, iv.Length, cipher, 0, fullCipher.Length - iv.Length);

            using (var aesAlg = Aes.Create())
            {
                aesAlg.Mode = mode;
                aesAlg.BlockSize = blockSize;
                aesAlg.Padding = PaddingMode.PKCS7;
                using (var decryptor = aesAlg.CreateDecryptor(symmetricEncryptionKey, iv))
                {
                    string result;
                    using (var msDecrypt = new MemoryStream(cipher))
                    {
                        using (var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                        {
                            using (var srDecrypt = new StreamReader(csDecrypt))
                            {
                                result = srDecrypt.ReadToEnd();
                            }
                        }
                    }

                    return result;
                }
            }
        }

        public async Task<List<Package>> GetEncryptedPackagesAsync(byte[] data, CipherMode cipherMode = CipherMode.CBC)
        {

            if (_dummyDataService.SendDummyDataOnly)
            {
                data = _dummyDataService.GetBytesDummyData();
            }
            var packages = DataPackager.DivideIntoPackages(data, 1024 * 1024 * 10);
            var random = new Random();
            foreach (var package in packages)
            {
                var bytes = new byte[16];
                random.NextBytes(bytes);
                package.Iv = bytes.ToList();

                package.Data = await _javaScriptInteropService.Encrypt(package.Data, _sessionKey, package.Iv, cipherMode);
            }

            return packages;
        }

        public async Task<string> GetMessageFromPackagesAsync()
        {
            if (_dummyDataService.SendDummyDataOnly)
            {
                return _dummyDataService.GetStringDummyData();
            }

            var sortedPackages = DataPackager.SortPackagesByIdList(_packageIds, _packages);
            foreach (var package in sortedPackages)
            {
                package.Data = await _javaScriptInteropService.Decrypt(package.Data, _sessionKey, package.Iv, _currentDataCipherMode);
            }
            var data = DataPackager.UnpackData(sortedPackages);
            _packageIds.Clear();
            _packages.Clear();
            return Encoding.UTF8.GetString(data);
        }

        public async Task<byte[]> GetBytesFromPackagesAsync()
        {
            if (_dummyDataService.SendDummyDataOnly)
            {
                return _dummyDataService.GetBytesDummyData();
            }

            var sortedPackages = DataPackager.SortPackagesByIdList(_packageIds, _packages);
            foreach (var package in sortedPackages)
            {
                package.Data = await _javaScriptInteropService.Decrypt(package.Data, _sessionKey, package.Iv, _currentDataCipherMode);
            }
            var data = DataPackager.UnpackData(sortedPackages);
            _packageIds.Clear();
            _packages.Clear();
            return data;
        }

        public void AddPackage(Package package)
        {
            _packages.Add(package);
        }

        public void AddPackageIds(List<Guid> packageIds)
        {
            _packageIds.AddRange(packageIds);
        }

        public void SetDataOptions(CipherMode cipherMode = CipherMode.CBC, int blockSize = 128)
        {
            _currentDataCipherMode = cipherMode;
            _currentDataBlockSize = blockSize;
        }

        public byte[] GenerateSessionKey()
        {
            using var aesAlg = Aes.Create();
            aesAlg.GenerateKey();
            return aesAlg.Key;
        }

        public void SetSessionKey(List<byte> key)
        {
            _sessionKey = key;
        }

        public void SetReceiverPublicKey(string key)
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(key));
            if (decoded == _dummyDataService.KeyDummyData)
            {
                _dummyDataService.SetReceiveDummyDataOnly();
            }
            _receiverPublicKey = Convert.FromBase64String(key).ToList();
        }

        public string GetSessionKeyEncryptedWithReceiverPublicKey()
        {
            if (_dummyDataService.ReceiveDummyDataOnly)
            {
                return Convert.ToBase64String(Encoding.UTF8.GetBytes(_dummyDataService.KeyDummyData));
            }
            var encryptedKeyBytes = EncryptWithRsa(_sessionKey, _receiverPublicKey);
            return Convert.ToBase64String(encryptedKeyBytes);
        }

        public (string PublicKey, string PrivateKey) GetKeys(string password)
        {
            var (hashedPassword, hashedPasswordBytes) = HashPassword(password);

            if (!_keyStore.KeysExists())
            {
                using var rsa = RSA.Create(512);
                var privateKey = rsa.ExportRSAPrivateKey();
                var privateKeyText = Convert.ToBase64String(privateKey);
                var publicKey = rsa.ExportRSAPublicKey();
                var publicKeyText = Convert.ToBase64String(publicKey);

                var encryptedPrivateCombinedKey = EncryptDataToBase64(privateKeyText, hashedPasswordBytes);
                var encryptedPublicCombinedKey = EncryptDataToBase64(publicKeyText, hashedPasswordBytes);

                _keyStore.SaveEncryptedKeys(encryptedPublicCombinedKey, encryptedPrivateCombinedKey, hashedPassword);

                _publicKey = publicKey;
                _privateKey = privateKey.ToList();

                return (publicKeyText, privateKeyText);

            }

            var (publicKeyBase64, privateKeyBase64) = _keyStore.GetEncryptedKeys(hashedPassword);
            if (publicKeyBase64 == string.Empty || privateKeyBase64 == string.Empty)
            {
                publicKeyBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(_dummyDataService.KeyDummyData));
                privateKeyBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(_dummyDataService.KeyDummyData));
            }
            var decryptedPrivateKey = string.Empty;
            var decryptedPublicKey = string.Empty;
            try
            {
                decryptedPrivateKey = DecryptDataFromBase64(privateKeyBase64, hashedPasswordBytes);
                decryptedPublicKey = DecryptDataFromBase64(publicKeyBase64, hashedPasswordBytes);
            }
            catch (CryptographicException e)
            {
                _dummyDataService.SetSendDummyDataOnly();
                var random = new Random();
                var bytes = new byte[128];
                random.NextBytes(bytes);
                decryptedPublicKey = Convert.ToBase64String(Encoding.UTF8.GetBytes(_dummyDataService.KeyDummyData));
                random.NextBytes(bytes);
                decryptedPrivateKey = Convert.ToBase64String(bytes);

            }

            return (decryptedPublicKey, decryptedPrivateKey);
        }

        public (string hashedPassword, byte[] hashedPasswordBytes) HashPassword(string password)
        {

            var sha = SHA256.Create();
            var hashedPasswordBytes = sha.ComputeHash(Encoding.UTF8.GetBytes(password));
            var hashedPassword = Encoding.UTF8.GetString(hashedPasswordBytes);
            var multiplyCounter = Math.Floor(256.0 / (hashedPasswordBytes.Length * 8)) + 1;
            var bytes = new List<byte>();
            Enumerable.Range(0, (int)multiplyCounter).ToList().ForEach(i => bytes.AddRange(hashedPasswordBytes));
            var key = bytes.Take(256 / 8).ToArray();

            return (hashedPassword, key);
        }
    }
}
