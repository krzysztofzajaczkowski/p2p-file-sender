using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using FileExchanger.Models;

namespace FileExchanger.Services.Encryptor
{
    public interface IEncryptorService
    {
        byte[] GenerateSessionKey();
        void SetSessionKey(List<byte> key);
        void SetReceiverPublicKey(string key);
        string GetSessionKeyEncryptedWithReceiverPublicKey();
        (string PublicKey, string PrivateKey) GetKeys(string password);
        byte[] DecryptSessionKeyWithRsa(string data);
        void AddPackage(Package package);
        void AddPackageIds(List<Guid> packageIds);
        void SetDataOptions(CipherMode cipherMode = CipherMode.CBC, int blockSize = 128);
        Task<List<Package>> GetEncryptedPackagesAsync(byte[] data, CipherMode cipherMode = CipherMode.CBC);
        Task<string> GetMessageFromPackagesAsync();
        Task<byte[]> GetBytesFromPackagesAsync();
        CipherMode CurrentDataCipherMode { get; }
        List<byte> SessionKey { get; }
        List<byte> PrivateKey { get; }
        byte[] PublicKey { get; }

        (string hashedPassword, byte[] hashedPasswordBytes) HashPassword(string password);
    }
}
