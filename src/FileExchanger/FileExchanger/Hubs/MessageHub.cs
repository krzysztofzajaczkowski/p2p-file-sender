using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using FileExchanger.Models;
using FileExchanger.Services.ConnectionStore;
using FileExchanger.Services.Encryptor;
using FileExchanger.Services.FileManager;
using Microsoft.AspNetCore.SignalR;

namespace FileExchanger.Hubs
{
    public class MessageHub : Hub
    {
        private readonly IEncryptorService _encryptorService;
        private readonly IFileManager _fileManager;
        private readonly IConnectionStore _connectionStore;
        private IClientProxy Host => Clients.Client(_connectionStore.HostId);
        private IClientProxy Caller => Clients.Caller;

        public MessageHub(IEncryptorService encryptorService, IFileManager fileManager, IConnectionStore connectionStore)
        {
            _encryptorService = encryptorService;
            _fileManager = fileManager;
            _connectionStore = connectionStore;

        }

        public Task SendMessageToAll(string message)
        {
            return Clients.All.SendAsync("ReceiveSendMessageToAll", message);
        }

        public Task CheckIfImHost()
        {
            return Clients.Caller.SendAsync("ReceiveCheckIfImHost", Context.ConnectionId == _connectionStore.HostId);
        }

        public async Task ConnectAsHost()
        {
            _connectionStore.SetHostId(Context.ConnectionId);
            await Clients.Caller.SendAsync("ReceiveConnectAsHost", true);
        }

        public Task SendMessage(string message)
        {
            return Clients.Client(_connectionStore.HostId).SendAsync("ReceiveSendMessage", message);
        }

        public async Task SendReceiverPublicKey(string key)
        {
            _encryptorService.SetReceiverPublicKey(key);
            await Caller.SendAsync("ReceiveSendReceiverPublicKey", true);
            await Host.SendAsync("ReceiveSendReceiverPublicKey", true);

        }

        public Task SendEncryptedSessionKey(string key)
        {
            var decryptedKey = _encryptorService.DecryptSessionKeyWithRsa(key);
            _encryptorService.SetSessionKey(decryptedKey.ToList());
            return Task.WhenAll(
                Host.SendAsync("ReceiveSendEncryptedSessionKey"),
                Caller.SendAsync("ReceiveSendEncryptedSessionKey")
            );
        }

        public Task StartSendingMessage(List<Guid> packageIds, int numberOfPackages, CipherMode cipherMode, int blockSize)
        {
            _encryptorService.AddPackageIds(packageIds);
            _encryptorService.SetDataOptions(cipherMode, blockSize);
            return Host.SendAsync("ReceiveStartSendingMessage", packageIds, numberOfPackages);
        }

        public async Task SendPackage(Package package)
        {
            await Caller.SendAsync("ReceiveProgress", package.Number);
            _encryptorService.AddPackage(package);
        }

        public async Task StopSendingMessage()
        {
            var message = await _encryptorService.GetMessageFromPackagesAsync();
            await Host.SendAsync("ReceiveStopSendingMessage", message);
        }

        public Task StartSendingFile(List<Guid> packageIds, int numberOfPackages, CipherMode cipherMode, int blockSize)
        {
            _encryptorService.AddPackageIds(packageIds);
            _encryptorService.SetDataOptions(cipherMode, blockSize);
            return Host.SendAsync("ReceiveStartSendingFile", packageIds, numberOfPackages);
        }

        public async Task StopSendingFile(string fileName)
        {
            var bytes = await _encryptorService.GetBytesFromPackagesAsync();
            _fileManager.SaveFile(bytes, fileName);
            await Host.SendAsync("ReceiveStopSendingFile", fileName);
        }

    }
}
