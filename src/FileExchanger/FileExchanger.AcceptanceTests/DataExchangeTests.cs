using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ductus.FluentDocker.Services;
using Ductus.FluentDocker.Services.Extensions;
using FileExchanger.Hubs;
using FileExchanger.Models;
using FileExchanger.Requests;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;
using Xunit;
using Xunit.Abstractions;

namespace FileExchanger.AcceptanceTests
{
    public class DataExchangeTests : DockerTestsBase, IDisposable
    {
        private
            (HttpClient httpClient,
            HubConnection hostConnection,
            HubConnection clientConnection)
            _firstClient;

        private
            (HttpClient httpClient,
            HubConnection hostConnection,
            HubConnection clientConnection)
            _secondClient;

        private readonly Task _clientsBootstrapTask;

        public DataExchangeTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
            _clientsBootstrapTask = Task.Run(async () =>
            {
                _firstClient = await SetupConnectionForBothServers("localhost:8006", "localhost:8008");
                _secondClient = await SetupConnectionForBothServers("localhost:8008", "localhost:8006");

                return Task.CompletedTask;
            });
        }

        [Theory]
        [InlineData(CipherMode.ECB)]
        [InlineData(CipherMode.CBC)]
        [InlineData(CipherMode.CFB)]
        [InlineData(CipherMode.OFB)]
        public async Task SendingText_WhenClientsExchangedKeys_SecondClientShouldReceiveCorrectMessage(CipherMode cipherMode)
        {
            // Arrange
            await _clientsBootstrapTask;
            var message = "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Aliquam vitae metus eget sapien vulputate fermentum et ac tellus. Donec erat quam, bibendum eget consequat sit amet, egestas ut urna. Praesent felis nunc, fermentum id turpis vitae, congue sagittis erat. Duis iaculis urna non volutpat auctor. Maecenas a sapien varius, auctor ipsum quis, interdum elit. Nunc luctus ornare orci, ac porttitor magna ornare id. Vivamus pretium cursus velit eget vulputate. Vestibulum tellus sem, fringilla non pharetra ac, egestas a nisl. Nunc vitae lectus orci. Lorem ipsum dolor sit amet, consectetur adipiscing elit. Donec mattis, ex ut porta ultricies, nisl arcu vehicula magna, eu laoreet elit elit auctor leo. Mauris eu blandit nunc. Proin hendrerit efficitur mattis. Proin blandit faucibus erat eu dictum. Nunc nec tortor pretium, auctor urna at, ullamcorper enim.\r\n\r\nVestibulum ante ipsum primis in faucibus orci luctus et ultrices posuere cubilia curae; Etiam semper, augue et gravida maximus, tellus tellus tempus sem, hendrerit in.";
            message = Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(message).Take(1023).ToArray());
            var receivedMessage = string.Empty;

            var firstClient = _firstClient;
            var firstClientPassword = "firstClientPassword";
            var firstClientResetEvent = new ManualResetEvent(false);
            var firstClientLoginResponse = await LoginClientAsync(firstClient.httpClient, firstClientPassword);
            firstClient.hostConnection.On<bool>("ReceiveSendReceiverPublicKey", (b) =>
            {
                firstClientResetEvent.Set();
            });
            firstClient.clientConnection.On("ReceiveSendEncryptedSessionKey", () =>
            {
                firstClientResetEvent.Set();
            });

            var secondClient = _secondClient;
            var secondClientPassword = "secondClientPassword";
            var secondClientResetEvent = new ManualResetEvent(false);
            var secondClientLoginResponse = await LoginClientAsync(secondClient.httpClient, secondClientPassword);
            secondClient.clientConnection.On<bool>("ReceiveSendReceiverPublicKey", (b) =>
            {
                secondClientResetEvent.Set();
            });
            secondClient.hostConnection.On("ReceiveSendEncryptedSessionKey", () =>
            {
                secondClientResetEvent.Set();
            });
            secondClient.hostConnection.On<string>("ReceiveStopSendingMessage", (msg) =>
            {
                receivedMessage = msg;
                secondClientResetEvent.Set();
            });

            // Act
            await secondClient.clientConnection.InvokeAsync(nameof(MessageHub.SendReceiverPublicKey), secondClientLoginResponse.PublicKey);
            secondClientResetEvent.WaitOne();
            firstClientResetEvent.WaitOne();
            secondClientResetEvent.Reset();
            firstClientResetEvent.Reset();

            var response = await firstClient.httpClient.GetAsync("api/exchange/key");
            var sessionKeyEncryptedWithPublicKey = await response.Content.ReadAsStringAsync();

            await firstClient.clientConnection.InvokeAsync(nameof(MessageHub.SendEncryptedSessionKey),
                sessionKeyEncryptedWithPublicKey);
            secondClientResetEvent.WaitOne();
            firstClientResetEvent.WaitOne();
            secondClientResetEvent.Reset();
            firstClientResetEvent.Reset();

            response = await firstClient.httpClient.PostAsJsonAsync<PackageTextRequest>("api/exchange/packageText", new PackageTextRequest
            {
                Text = message,
                Mode = cipherMode
            });
            var packages = await response.Content.ReadFromJsonAsync<List<Package>>();

            await firstClient.clientConnection.InvokeAsync(nameof(MessageHub.StartSendingMessage), packages.Select(p => p.Id).ToList(),
                packages.Count, cipherMode, 128);
            foreach (var package in packages)
            {
                await firstClient.clientConnection.InvokeAsync(nameof(MessageHub.SendPackage), package);
            }
            await firstClient.clientConnection.InvokeAsync(nameof(MessageHub.StopSendingMessage));
            secondClientResetEvent.WaitOne();

            // Assert
            receivedMessage.Should().Be(message);
        }

        [Theory]
        [InlineData(CipherMode.ECB, 0)]
        [InlineData(CipherMode.CBC, 0)]
        [InlineData(CipherMode.CFB, 0)]
        [InlineData(CipherMode.OFB, 0)]
        [InlineData(CipherMode.ECB, 13)]
        [InlineData(CipherMode.CBC, 13)]
        [InlineData(CipherMode.CFB, 13)]
        [InlineData(CipherMode.OFB, 13)]
        [InlineData(CipherMode.ECB, 1022)]
        [InlineData(CipherMode.CBC, 1022)]
        [InlineData(CipherMode.CFB, 1022)]
        [InlineData(CipherMode.OFB, 1022)]
        [InlineData(CipherMode.ECB, 1024 * 1024)]
        [InlineData(CipherMode.CBC, 1024 * 1024)]
        [InlineData(CipherMode.CFB, 1024 * 1024)]
        [InlineData(CipherMode.OFB, 1024 * 1024)]
        [InlineData(CipherMode.ECB, 1024 * 1024 * 15)]
        [InlineData(CipherMode.CBC, 1024 * 1024 * 15)]
        [InlineData(CipherMode.CFB, 1024 * 1024 * 15)]
        [InlineData(CipherMode.OFB, 1024 * 1024 * 15)]
        [InlineData(CipherMode.ECB, 1024 * 1024 * 30)]
        [InlineData(CipherMode.CBC, 1024 * 1024 * 30)]
        [InlineData(CipherMode.CFB, 1024 * 1024 * 30)]
        [InlineData(CipherMode.OFB, 1024 * 1024 * 30)]
        [InlineData(CipherMode.ECB, 1024 * 1024 * 50)]
        [InlineData(CipherMode.CBC, 1024 * 1024 * 50)]
        [InlineData(CipherMode.CFB, 1024 * 1024 * 50)]
        [InlineData(CipherMode.OFB, 1024 * 1024 * 50)]
        [InlineData(CipherMode.ECB, 1024 * 1024 * 100)]
        [InlineData(CipherMode.CBC, 1024 * 1024 * 100)]
        [InlineData(CipherMode.CFB, 1024 * 1024 * 100)]
        [InlineData(CipherMode.OFB, 1024 * 1024 * 100)]
        public async Task SendingFile_WhenClientsExchangedKeys_FirstClientShouldReceiveCorrectFile(CipherMode cipherMode, int sizeInBytes)
        {
            // Arrange
            await _clientsBootstrapTask;
            var stopwatch = new Stopwatch();
            var originalBytes = new byte[sizeInBytes];
            new Random().NextBytes(originalBytes);
            var fileName = "testFile.test";
            var receivedFileName = string.Empty;

            var firstClient = _firstClient;
            var firstClientPassword = "firstClientPassword";
            var firstClientResetEvent = new ManualResetEvent(false);
            var firstClientLoginResponse = await LoginClientAsync(firstClient.httpClient, firstClientPassword);
            firstClient.hostConnection.On<bool>("ReceiveSendReceiverPublicKey", (b) =>
            {
                firstClientResetEvent.Set();
            });
            firstClient.clientConnection.On("ReceiveSendEncryptedSessionKey", () =>
            {
                firstClientResetEvent.Set();
            });
            firstClient.hostConnection.On<string>("ReceiveStopSendingFile", (fileName) =>
            {
                receivedFileName = fileName;
                firstClientResetEvent.Set();
            });

            var secondClient = _secondClient;
            var secondClientPassword = "secondClientPassword";
            var secondClientResetEvent = new ManualResetEvent(false);
            var secondClientLoginResponse = await LoginClientAsync(secondClient.httpClient, secondClientPassword);
            secondClient.clientConnection.On<bool>("ReceiveSendReceiverPublicKey", (b) =>
            {
                secondClientResetEvent.Set();
            });
            secondClient.hostConnection.On("ReceiveSendEncryptedSessionKey", () =>
            {
                secondClientResetEvent.Set();
            });


            // Act
            await secondClient.clientConnection.InvokeAsync(nameof(MessageHub.SendReceiverPublicKey), secondClientLoginResponse.PublicKey);
            secondClientResetEvent.WaitOne();
            firstClientResetEvent.WaitOne();
            secondClientResetEvent.Reset();
            firstClientResetEvent.Reset();

            var response = await firstClient.httpClient.GetAsync("api/exchange/key");
            var sessionKeyEncryptedWithPublicKey = await response.Content.ReadAsStringAsync();

            await firstClient.clientConnection.InvokeAsync(nameof(MessageHub.SendEncryptedSessionKey),
                sessionKeyEncryptedWithPublicKey);
            secondClientResetEvent.WaitOne();
            firstClientResetEvent.WaitOne();
            secondClientResetEvent.Reset();
            firstClientResetEvent.Reset();

            await using var file1 = new MemoryStream(originalBytes);
            using var content1 = new StreamContent(file1);
            using var formData = new MultipartFormDataContent
            {
                {content1, "files", "name"},
                {new StringContent(cipherMode.ToString()), "mode"}
            };

            stopwatch.Restart();
            response = await secondClient.httpClient.PostAsync("api/exchange/packageFile", formData);
            var packages = await response.Content.ReadFromJsonAsync<List<Package>>();
            TestOutputHelper.WriteLine(
                $"Packaging file of size {sizeInBytes} bytes took {stopwatch.ElapsedMilliseconds} ms.");
            stopwatch.Restart();
            await secondClient.clientConnection.InvokeAsync(nameof(MessageHub.StartSendingFile), packages.Select(p => p.Id).ToList(),
            packages.Count, cipherMode, 128);
            foreach (var package in packages)
            {
                await secondClient.clientConnection.InvokeAsync(nameof(MessageHub.SendPackage), package);
            }

            await secondClient.clientConnection.InvokeAsync(nameof(MessageHub.StopSendingFile), fileName);
            firstClientResetEvent.WaitOne();
            TestOutputHelper.WriteLine(
                $"Exchanging file of size {sizeInBytes} bytes took {stopwatch.ElapsedMilliseconds} ms.");
            // Assert
            receivedFileName.Should().Be(fileName);

            // Assert file exists in container
            var command = CompositeService.Containers.First().Execute($"test -f /home/files/{receivedFileName}");
            command.Success.Should().BeTrue();

            // Assert received content is equal to original content
            var receivedBytes = await GetFileBytesFromContainer(CompositeService.Containers.First(), receivedFileName);
            receivedBytes.Should().Equal(originalBytes);
        }

        [Theory]
        [InlineData(CipherMode.ECB,
            new[] { 0, 1, 10, 15, 23, 54, 72, 80, 111, 727, 1024 * 512, 1024 * 1024, 1024 * 1024 * 3 },
            new[] { 1, 2, 15, 16, 126, 512, 1024, 1024 * 1024, (int)(1024 * 1024 * 1.5) })]
        [InlineData(CipherMode.CBC,
            new[] { 0, 1, 10, 15, 23, 54, 72, 80, 111, 727, 1024 * 512, 1024 * 1024, 1024 * 1024 * 3 },
            new[] { 1, 2, 15, 16, 126, 512, 1024, 1024 * 1024, (int)(1024 * 1024 * 1.5) })]
        [InlineData(CipherMode.CFB,
            new[] { 0, 1, 10, 15, 23, 54, 72, 80, 111, 727, 1024 * 512, 1024 * 1024, 1024 * 1024 * 3 },
            new[] { 1, 2, 15, 16, 126, 512, 1024, 1024 * 1024, (int)(1024 * 1024 * 1.5) })]
        [InlineData(CipherMode.OFB,
            new[] { 0, 1, 10, 15, 23, 54, 72, 80, 111, 727, 1024 * 512, 1024 * 1024, 1024 * 1024 * 3 },
            new[] { 1, 2, 15, 16, 126, 512, 1024, 1024 * 1024, (int)(1024 * 1024 * 1.5) })]
        public async Task MessagesAndFilesExchange_WhenClientsExchangedKeys_BothClientsShouldReceiveMessagesAndFiles(CipherMode cipherMode, int[] messageSizesInBytes, int[] fileSizesInBytes)
        {
            // Arrange
            await _clientsBootstrapTask;
            var receivedFileName = string.Empty;
            var fileName = "testFile";
            var chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var random = new Random();
            Func<int, string> generateMessage = (length) =>
            {
                return new string(Enumerable.Repeat(chars, length)
                    .Select(s => s[random.Next(s.Length)]).ToArray());
            };
            Func<int, byte[]> generateFile = (length) =>
            {
                var bytes = new byte[length];
                random.NextBytes(bytes);
                return bytes;
            };
            var messages = messageSizesInBytes.Select(generateMessage).ToList();

            var files = fileSizesInBytes.Select(generateFile).ToList();
            var fileNames = new List<string>();

            for (var i = 0; i < files.Count; i++)
            {
                fileNames.Add($"{fileName}{i}.txt");
            }

            var receivedMessage = string.Empty;

            var firstReceivedMessages = new List<string>();
            var firstReceivedFiles = new List<byte[]>();
            var firstReceivedFileNames = new List<string>();
            var firstClient = _firstClient;
            var firstClientPassword = "firstClientPassword";
            var firstClientResetEvent = new ManualResetEvent(false);
            var firstClientLoginResponse = await LoginClientAsync(firstClient.httpClient, firstClientPassword);
            firstClient.hostConnection.On<bool>("ReceiveSendReceiverPublicKey",
                (b) => { firstClientResetEvent.Set(); });
            firstClient.clientConnection.On("ReceiveSendEncryptedSessionKey", () => { firstClientResetEvent.Set(); });
            firstClient.hostConnection.On<string>("ReceiveStopSendingMessage", (msg) =>
            {
                receivedMessage = msg;
                firstClientResetEvent.Set();
            });
            firstClient.hostConnection.On<string>("ReceiveStopSendingFile", (fileName) =>
            {
                receivedFileName = fileName;
                firstClientResetEvent.Set();
            });

            var secondReceivedMessages = new List<string>();
            var secondReceivedFiles = new List<byte[]>();
            var secondReceivedFileNames = new List<string>();
            var secondClient = _secondClient;
            var secondClientPassword = "secondClientPassword";
            var secondClientResetEvent = new ManualResetEvent(false);
            var secondClientLoginResponse = await LoginClientAsync(secondClient.httpClient, secondClientPassword);
            secondClient.clientConnection.On<bool>("ReceiveSendReceiverPublicKey",
                (b) => { secondClientResetEvent.Set(); });
            secondClient.hostConnection.On("ReceiveSendEncryptedSessionKey", () => { secondClientResetEvent.Set(); });
            secondClient.hostConnection.On<string>("ReceiveStopSendingMessage", (msg) =>
            {
                receivedMessage = msg;
                secondClientResetEvent.Set();
            });
            secondClient.hostConnection.On<string>("ReceiveStopSendingFile", (fileName) =>
            {
                receivedFileName = fileName;
                secondClientResetEvent.Set();
            });

            // Act
            await secondClient.clientConnection.InvokeAsync(nameof(MessageHub.SendReceiverPublicKey),
                secondClientLoginResponse.PublicKey);
            secondClientResetEvent.WaitOne();
            firstClientResetEvent.WaitOne();
            secondClientResetEvent.Reset();
            firstClientResetEvent.Reset();

            var response = await firstClient.httpClient.GetAsync("api/exchange/key");
            var sessionKeyEncryptedWithPublicKey = await response.Content.ReadAsStringAsync();

            await firstClient.clientConnection.InvokeAsync(nameof(MessageHub.SendEncryptedSessionKey),
                sessionKeyEncryptedWithPublicKey);
            secondClientResetEvent.WaitOne();
            firstClientResetEvent.WaitOne();
            secondClientResetEvent.Reset();
            firstClientResetEvent.Reset();

            for (var i = 0; i < messages.Count; i++)
            {
                var msg = messages[i];
                (HttpClient httpClient, HubConnection hostConnection, HubConnection clientConnection) sender;
                List<string> receiverReceivedMessages;
                ManualResetEvent receiverResetEvent;
                if (i % 2 == 0)
                {
                    sender = firstClient;
                    receiverResetEvent = secondClientResetEvent;
                    receiverReceivedMessages = secondReceivedMessages;
                }
                else
                {
                    sender = secondClient;
                    receiverResetEvent = firstClientResetEvent;
                    receiverReceivedMessages = firstReceivedMessages;
                }
                response = await sender.httpClient.PostAsJsonAsync<PackageTextRequest>("api/exchange/packageText",
                    new PackageTextRequest
                    {
                        Text = msg,
                        Mode = cipherMode
                    });
                var packages = await response.Content.ReadFromJsonAsync<List<Package>>();

                await sender.clientConnection.InvokeAsync(nameof(MessageHub.StartSendingMessage),
                    packages.Select(p => p.Id).ToList(),
                    packages.Count, cipherMode, 128);
                foreach (var package in packages)
                {
                    await sender.clientConnection.InvokeAsync(nameof(MessageHub.SendPackage), (object)package);
                }

                await sender.clientConnection.InvokeAsync(nameof(MessageHub.StopSendingMessage));
                receiverResetEvent.WaitOne();
                receiverResetEvent.Reset();
                receiverReceivedMessages.Add(receivedMessage);
            }

            for (var i = 0; i < files.Count; i++)
            {
                var file = files[i];
                (HttpClient httpClient, HubConnection hostConnection, HubConnection clientConnection) sender;
                ManualResetEvent receiverResetEvent;
                List<byte[]> receiverReceivedFiles;
                List<string> receiverReceivedFileNames;
                IContainerService receiverContainer;
                if (i % 2 == 0)
                {
                    sender = firstClient;
                    receiverResetEvent = secondClientResetEvent;
                    receiverReceivedFiles = secondReceivedFiles;
                    receiverReceivedFileNames = secondReceivedFileNames;
                    receiverContainer = CompositeService.Containers.Last();
                }
                else
                {
                    sender = secondClient;
                    receiverResetEvent = firstClientResetEvent;
                    receiverReceivedFiles = firstReceivedFiles;
                    receiverReceivedFileNames = firstReceivedFileNames;
                    receiverContainer = CompositeService.Containers.First();
                }
                await using var file1 = new MemoryStream(file);
                using var content1 = new StreamContent(file1);
                using var formData = new MultipartFormDataContent
                {
                    {content1, "files", "name"}, {new StringContent(cipherMode.ToString()), "mode"}
                };

                response = await sender.httpClient.PostAsync("api/exchange/packageFile", formData);
                var packages = await response.Content.ReadFromJsonAsync<List<Package>>();

                await sender.clientConnection.InvokeAsync(nameof(MessageHub.StartSendingFile),
                    packages.Select(p => p.Id).ToList(),
                    packages.Count, cipherMode, 128);
                foreach (var package in packages)
                {
                    await sender.clientConnection.InvokeAsync(nameof(MessageHub.SendPackage), (object)package);
                }

                await sender.clientConnection.InvokeAsync(nameof(MessageHub.StopSendingFile), fileNames[i]);
                receiverResetEvent.WaitOne();
                receiverResetEvent.Reset();
                receiverReceivedFiles.Add(await GetFileBytesFromContainer(receiverContainer, receivedFileName));
                receiverReceivedFileNames.Add(receivedFileName);
            }

            // Assert
            firstReceivedMessages.Should().Equal(messages.Where((m, i) => i % 2 == 1));
            secondReceivedMessages.Should().Equal(messages.Where((m, i) => i % 2 == 0));

            firstReceivedFiles.Should().BeEquivalentTo(files.Where((m, i) => i % 2 == 1));
            firstReceivedFileNames.Should().Equal(fileNames.Where((n, i) => i % 2 == 1));

            secondReceivedFiles.Should().BeEquivalentTo(files.Where((m, i) => i % 2 == 0));
            secondReceivedFileNames.Should().Equal(fileNames.Where((n, i) => i % 2 == 0));

        }

        public new void Dispose()
        {
            _firstClient.clientConnection.DisposeAsync();
            _firstClient.hostConnection.DisposeAsync();
            _firstClient.hostConnection.DisposeAsync();
            _secondClient.clientConnection.DisposeAsync();
            _secondClient.hostConnection.DisposeAsync();
            _secondClient.hostConnection.DisposeAsync();
            base.Dispose();
        }
    }
}
