using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ElectronNET.API.Entities;
using FileExchanger.Helpers;
using FileExchanger.Hubs;
using FileExchanger.Models;
using FileExchanger.Requests;
using FileExchanger.Responses;
using FileExchanger.Services.DummyData;
using FileExchanger.Services.Encryptor;
using FileExchanger.Services.FileManager;
using FileExchanger.Services.KeyStore;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace FileExchanger.IntegrationTests
{
    public class MessageHubTests : BaseTest, IDisposable
    {
        private readonly ITestOutputHelper _testOutputHelper;
        private TestServer _firstServer;
        private TestServer _secondServer;


        public MessageHubTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        [Fact]
        public async Task SendToAll_WhenInvoked_ShouldReplyWithTheSameMessage()
        {
            // Arrange
            var messageReceivedEvent = new ManualResetEvent(false);
            const string message = "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Curabitur interdum quis enim a maximus.";
            var receivedMessage = string.Empty;

            var server = BuildTestServer(clientName: "firstServer");
            _firstServer = server;
            var client = BuildHubConnection(server);

            client.On<string>("ReceiveSendMessageToAll", msg =>
            {
                receivedMessage = msg;
                messageReceivedEvent.Set();
            });

            // Act
            await client.StartAsync();
            await client.InvokeAsync(nameof(MessageHub.SendMessageToAll), message);

            messageReceivedEvent.WaitOne();

            // Assert
            receivedMessage.Should().Be(message);
        }

        [Fact]
        public async Task CheckIfImHost_WhenHostNotSet_ShouldReturnFalse()
        {
            // Arrange
            var messageReceivedEvent = new ManualResetEvent(false);
            bool? isHost = null;
            var server = BuildTestServer(clientName: "firstServer");
            _firstServer = server;
            var client = BuildHubConnection(server);

            client.On<bool>("ReceiveCheckIfImHost", msg =>
            {
                isHost = msg;
                messageReceivedEvent.Set();
            });

            // Act
            await client.StartAsync();
            await client.InvokeAsync(nameof(MessageHub.CheckIfImHost));

            messageReceivedEvent.WaitOne();

            // Assert
            isHost.Should().BeFalse();
        }

        [Fact]
        public async Task CheckIfImHost_WhenConnectedAsHost_ShouldReturnTrue()
        {
            // Arrange
            var messageReceivedEvent = new ManualResetEvent(false);
            bool? isHost = null;

            var server = BuildTestServer(clientName: "firstServer");
            _firstServer = server;
            var client = BuildHubConnection(server);

            client.On<bool>("ReceiveCheckIfImHost", msg =>
            {
                isHost = msg;
                messageReceivedEvent.Set();
            });

            // Act
            await client.StartAsync();
            await client.InvokeAsync(nameof(MessageHub.ConnectAsHost));
            await client.InvokeAsync(nameof(MessageHub.CheckIfImHost));

            messageReceivedEvent.WaitOne();

            // Assert
            isHost.Should().BeTrue();
        }

        [Fact]
        public async Task CheckIfImHost_WhenConnectedAsClientToOtherServer_ShouldReturnFalse()
        {
            // Arrange
            var messageReceivedInFirstClientEvent = new ManualResetEvent(false);
            var messageReceivedInSecondClientEvent = new ManualResetEvent(false);

            bool? isFirstClientHost = null;
            bool? isSecondClientHost = null;

            var server = BuildTestServer(clientName: "firstServer");
            _firstServer = server;
            var firstClient = BuildHubConnection(server);
            var secondClient = BuildHubConnection(server);

            firstClient.On<bool>("ReceiveCheckIfImHost", msg =>
            {
                isFirstClientHost = msg;
                messageReceivedInFirstClientEvent.Set();
            });

            secondClient.On<bool>("ReceiveCheckIfImHost", msg =>
            {
                isSecondClientHost = msg;
                messageReceivedInSecondClientEvent.Set();
            });

            // Act
            await firstClient.StartAsync();
            await firstClient.InvokeAsync(nameof(MessageHub.ConnectAsHost));
            await firstClient.InvokeAsync(nameof(MessageHub.CheckIfImHost));

            await secondClient.StartAsync();
            await secondClient.InvokeAsync(nameof(MessageHub.CheckIfImHost));

            messageReceivedInFirstClientEvent.WaitOne();
            messageReceivedInSecondClientEvent.WaitOne();

            // Assert
            isFirstClientHost.Should().BeTrue();
            isSecondClientHost.Should().BeFalse();
        }

        [Fact]
        public async Task SendMessage_WhenSecondClientInvoked_FirstClientShouldReceiveMessage()
        {
            // Arrange
            var message = "Test message from 2nd client";
            var receivedMessage = string.Empty;
            bool? firstClientIsHost = null;
            var firstClientConnectedAsHostEvent = new ManualResetEvent(false);
            var firstClientReceivedMessageEvent = new ManualResetEvent(false);

            var server = BuildTestServer(clientName: "firstServer");
            _firstServer = server;
            var firstConnection = BuildHubConnection(server);
            var secondConnection = BuildHubConnection(server);

            firstConnection.On<bool>("ReceiveConnectAsHost", msg =>
            {
                firstClientIsHost = true;
                firstClientConnectedAsHostEvent.Set();
            });

            firstConnection.On<string>("ReceiveSendMessage", msg =>
            {
                receivedMessage = msg;
                firstClientReceivedMessageEvent.Set();
            });

            // Act
            await firstConnection.StartAsync();
            await secondConnection.StartAsync();

            await firstConnection.InvokeAsync(nameof(MessageHub.ConnectAsHost));
            firstClientConnectedAsHostEvent.WaitOne();

            await secondConnection.InvokeAsync(nameof(MessageHub.SendMessage), message);
            firstClientReceivedMessageEvent.WaitOne();

            // Assert
            firstClientIsHost.Should().BeTrue();
            receivedMessage.Should().Be(message);
        }

        [Fact]
        public async Task LoggingIn_WhenUsingValidPassword_ShouldReturnSameKeyPair()
        {
            // Arrange
            var password = "TestPassword";
            var server = BuildTestServer(clientName: "firstServer");
            _firstServer = server;
            var client = CreateClientConnection(server);

            // Act
            var response = await client.httpClient.PostAsJsonAsync("api/account", new LoginRequest
            {
                Password = password
            });
            var firstLoginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>();

            response = await client.httpClient.PostAsJsonAsync("api/account", new LoginRequest
            {
                Password = password
            });
            var secondLoginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>();

            // Assert
            firstLoginResponse.PublicKey.Should().NotBeNullOrEmpty();
            secondLoginResponse.PublicKey.Should().Be(firstLoginResponse.PublicKey);
        }

        [Fact]
        public async Task RetrievedKeyPairs_ForTwoServers_ShouldNotBeEqual()
        {
            // Arrange
            var firstServer = BuildTestServer(clientName: "firstServer");
            _firstServer = firstServer;
            var secondServer = BuildTestServer(builder =>
            {
                builder.UseUrls("http://localhost:24555", "https://localhost:24556");
            }, "secondServer");
            _secondServer = secondServer;

            var firstClient = CreateConnectionsForBothServers(firstServer, secondServer);
            var firstClientPassword = "firstClientPassword";
            var firstClientResetEvent = new ManualResetEvent(false);
            await firstClient.hostConnection.StartAsync();
            await firstClient.clientConnection.StartAsync();

            var secondClient = CreateConnectionsForBothServers(secondServer, firstServer);
            var secondClientPassword = "secondClientPassword";
            var secondClientResetEvent = new ManualResetEvent(false);
            await secondClient.hostConnection.StartAsync();
            await secondClient.clientConnection.StartAsync();

            // Act
            var response = await firstClient.httpClient.PostAsJsonAsync("api/account", new LoginRequest
            {
                Password = firstClientPassword
            });
            var firstClientLoginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>();

            response = await secondClient.httpClient.PostAsJsonAsync("api/account", new LoginRequest
            {
                Password = secondClientPassword
            });
            var secondClientLoginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>();

            // Assert
            firstClientLoginResponse.PublicKey.Should().NotBeNullOrEmpty();
            secondClientLoginResponse.PublicKey.Should().NotBeNullOrEmpty();
            firstClientLoginResponse.PublicKey.Should().NotBe(secondClientLoginResponse.PublicKey);
        }

        [Fact]
        public async Task
            SendReceiverPublicKey_WhenSecondClientSendPublicKey_BothClientsShouldReceiveMessageReceiveSendReceiverPublicKey()
        {
            // Arrange
            var firstServer = BuildTestServer(clientName: "firstServer");
            _firstServer = firstServer;
            var secondServer = BuildTestServer(builder =>
            {
                builder.UseUrls("http://localhost:24555", "https://localhost:24556");
            }, "secondServer");
            _secondServer = secondServer;

            var firstClient = await SetupConnectionsForBothServersAsync(firstServer, secondServer);
            var firstClientPassword = "firstClientPassword";
            var firstClientResetEvent = new ManualResetEvent(false);
            var firstClientLoginResponse = await LoginClientAsync(firstClient.httpClient, firstClientPassword);

            var secondClient = await SetupConnectionsForBothServersAsync(secondServer, firstServer);
            var secondClientPassword = "secondClientPassword";
            var secondClientResetEvent = new ManualResetEvent(false);
            var secondClientLoginResponse = await LoginClientAsync(secondClient.httpClient, secondClientPassword);

            var firstClientReceivedInformationAboutPublicKey = false;
            var secondClientReceivedInformationAboutPublicKey = false;

            firstClient.hostConnection.On<bool>("ReceiveSendReceiverPublicKey", (b) =>
            {
                firstClientReceivedInformationAboutPublicKey = true;
                firstClientResetEvent.Set();
            });

            secondClient.clientConnection.On<bool>("ReceiveSendReceiverPublicKey", (b) =>
            {
                secondClientReceivedInformationAboutPublicKey = true;
                secondClientResetEvent.Set();
            });

            // Act
            await secondClient.clientConnection.InvokeAsync(nameof(MessageHub.SendReceiverPublicKey), secondClientLoginResponse.PublicKey);
            secondClientResetEvent.WaitOne();
            firstClientResetEvent.WaitOne();

            // Assert
            firstClientReceivedInformationAboutPublicKey.Should().BeTrue();
            secondClientReceivedInformationAboutPublicKey.Should().BeTrue();
        }

        [Fact]
        public async Task
            GeneratingSessionKey_WhenSecondClientSentPublicKey_SessionKeyShouldNotBePlainText()
        {
            // Arrange
            var firstServer = BuildTestServer(clientName: "firstServer");
            _firstServer = firstServer;
            var secondServer =
                BuildTestServer(builder => { builder.UseUrls("http://localhost:24555", "https://localhost:24556"); },
                    "secondServer");
            _secondServer = secondServer;

            var encryptor = firstServer.Services.GetRequiredService<IEncryptorService>();
            var firstClient = await SetupConnectionsForBothServersAsync(firstServer, secondServer);
            var firstClientPassword = "firstClientPassword";
            var firstClientResetEvent = new ManualResetEvent(false);
            var firstClientLoginResponse = await LoginClientAsync(firstClient.httpClient, firstClientPassword);

            var secondClient = await SetupConnectionsForBothServersAsync(secondServer, firstServer);
            var secondClientPassword = "secondClientPassword";
            var secondClientResetEvent = new ManualResetEvent(false);
            var secondClientLoginResponse = await LoginClientAsync(secondClient.httpClient, secondClientPassword);

            firstClient.hostConnection.On<bool>("ReceiveSendReceiverPublicKey",
                (b) => { firstClientResetEvent.Set(); });

            secondClient.clientConnection.On<bool>("ReceiveSendReceiverPublicKey",
                (b) => { secondClientResetEvent.Set(); });

            // Act
            await secondClient.clientConnection.InvokeAsync(nameof(MessageHub.SendReceiverPublicKey),
                secondClientLoginResponse.PublicKey);
            secondClientResetEvent.WaitOne();
            firstClientResetEvent.WaitOne();
            secondClientResetEvent.Reset();
            firstClientResetEvent.Reset();

            var response = await firstClient.httpClient.GetAsync("api/exchange/key");
            var sessionKeyEncryptedWithPublicKey = await response.Content.ReadAsStringAsync();

            // Assert
            encryptor.SessionKey.Should().NotEqual(Convert.FromBase64String(sessionKeyEncryptedWithPublicKey));
        }

        [Fact]
        public async Task
            GeneratingSessionKeyTwice_WhenSecondClientSentPublicKey_SessionKeysShouldNotBeEqual()
        {
            // Arrange
            List<byte> firstSessionKey;
            List<byte> secondSessionKey;
            var firstServer = BuildTestServer(clientName: "firstServer");
            _firstServer = firstServer;
            var secondServer =
                BuildTestServer(builder => { builder.UseUrls("http://localhost:24555", "https://localhost:24556"); },
                    "secondServer");
            _secondServer = secondServer;

            var encryptor = firstServer.Services.GetRequiredService<IEncryptorService>();
            var firstClient = await SetupConnectionsForBothServersAsync(firstServer, secondServer);
            var firstClientPassword = "firstClientPassword";
            var firstClientResetEvent = new ManualResetEvent(false);
            var firstClientLoginResponse = await LoginClientAsync(firstClient.httpClient, firstClientPassword);

            var secondClient = await SetupConnectionsForBothServersAsync(secondServer, firstServer);
            var secondClientPassword = "secondClientPassword";
            var secondClientResetEvent = new ManualResetEvent(false);
            var secondClientLoginResponse = await LoginClientAsync(secondClient.httpClient, secondClientPassword);

            firstClient.hostConnection.On<bool>("ReceiveSendReceiverPublicKey",
                (b) => { firstClientResetEvent.Set(); });

            secondClient.clientConnection.On<bool>("ReceiveSendReceiverPublicKey",
                (b) => { secondClientResetEvent.Set(); });

            // Act
            await secondClient.clientConnection.InvokeAsync(nameof(MessageHub.SendReceiverPublicKey),
                secondClientLoginResponse.PublicKey);
            secondClientResetEvent.WaitOne();
            firstClientResetEvent.WaitOne();
            secondClientResetEvent.Reset();
            firstClientResetEvent.Reset();

            var response = await firstClient.httpClient.GetAsync("api/exchange/key");
            var sessionKeyEncryptedWithPublicKey = await response.Content.ReadAsStringAsync();
            firstSessionKey = encryptor.SessionKey;

            response = await firstClient.httpClient.GetAsync("api/exchange/key");
            sessionKeyEncryptedWithPublicKey = await response.Content.ReadAsStringAsync();
            secondSessionKey = encryptor.SessionKey;

            // Assert
            secondSessionKey.Should().NotEqual(firstSessionKey);
        }

        [Fact]
        public async Task
            SendReceiverPublicKey_WhenFirstClientSendSessionKey_BothClientsShouldReceiveMessageReceiveSendEncryptedSessionKey()
        {
            // Arrange
            var firstClientReceivedInformationAboutSessionKey = false;
            var secondClientReceivedInformationAboutSessionKey = false;
            var firstServer = BuildTestServer(clientName: "firstServer");
            _firstServer = firstServer;
            var secondServer = BuildTestServer(builder =>
            {
                builder.UseUrls("http://localhost:24555", "https://localhost:24556");
            }, "secondServer");
            _secondServer = secondServer;

            var firstClient = await SetupConnectionsForBothServersAsync(firstServer, secondServer);
            var firstClientPassword = "firstClientPassword";
            var firstClientResetEvent = new ManualResetEvent(false);
            var firstClientLoginResponse = await LoginClientAsync(firstClient.httpClient, firstClientPassword);

            var secondClient = await SetupConnectionsForBothServersAsync(secondServer, firstServer);
            var secondClientPassword = "secondClientPassword";
            var secondClientResetEvent = new ManualResetEvent(false);
            var secondClientLoginResponse = await LoginClientAsync(secondClient.httpClient, secondClientPassword);

            firstClient.hostConnection.On<bool>("ReceiveSendReceiverPublicKey", (b) =>
            {
                firstClientResetEvent.Set();
            });

            secondClient.clientConnection.On<bool>("ReceiveSendReceiverPublicKey", (b) =>
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

            firstClient.clientConnection.On("ReceiveSendEncryptedSessionKey", () =>
            {
                firstClientReceivedInformationAboutSessionKey = true;
                firstClientResetEvent.Set();
            });
            secondClient.hostConnection.On("ReceiveSendEncryptedSessionKey", () =>
            {
                secondClientReceivedInformationAboutSessionKey = true;
                secondClientResetEvent.Set();
            });
            await firstClient.clientConnection.InvokeAsync(nameof(MessageHub.SendEncryptedSessionKey),
                sessionKeyEncryptedWithPublicKey);

            secondClientResetEvent.WaitOne();
            firstClientResetEvent.WaitOne();

            // Assert
            firstClientReceivedInformationAboutSessionKey.Should().BeTrue();
            secondClientReceivedInformationAboutSessionKey.Should().BeTrue();
        }

        [Fact]
        public async Task PackagingText_WhenSessionKeyGenerated_ShouldReturnPackagesOfEncryptedText()
        {
            // Arrange
            var message = "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Aliquam vitae metus eget sapien vulputate fermentum et ac tellus. Donec erat quam, bibendum eget consequat sit amet, egestas ut urna. Praesent felis nunc, fermentum id turpis vitae, congue sagittis erat. Duis iaculis urna non volutpat auctor. Maecenas a sapien varius, auctor ipsum quis, interdum elit. Nunc luctus ornare orci, ac porttitor magna ornare id. Vivamus pretium cursus velit eget vulputate. Vestibulum tellus sem, fringilla non pharetra ac, egestas a nisl. Nunc vitae lectus orci. Lorem ipsum dolor sit amet, consectetur adipiscing elit. Donec mattis, ex ut porta ultricies, nisl arcu vehicula magna, eu laoreet elit elit auctor leo. Mauris eu blandit nunc. Proin hendrerit efficitur mattis. Proin blandit faucibus erat eu dictum. Nunc nec tortor pretium, auctor urna at, ullamcorper enim.\r\n\r\nVestibulum ante ipsum primis in faucibus orci luctus et ultrices posuere cubilia curae; Etiam semper, augue et gravida maximus, tellus tellus tempus sem, hendrerit in.";
            var server = BuildTestServer(clientName: "firstServer");
            _firstServer = server;
            var client = CreateClientConnection(server);
            var clientPassword = "firstClientPassword";
            var clientResetEvent = new ManualResetEvent(false);
            var clientLoginResponse = await LoginClientAsync(client.httpClient, clientPassword);

            // Act
            var response = await client.httpClient.GetAsync("api/exchange/key");
            response = await client.httpClient.PostAsJsonAsync<PackageTextRequest>("api/exchange/packageText", new PackageTextRequest
            {
                Text = message
            });
            var packages = await response.Content.ReadFromJsonAsync<List<Package>>();

            // Assert
            packages.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task PackagingFile_WhenSessionKeyGenerated_ShouldReturnPackagesOfEncryptedFile()
        {
            // Arrange
            var message = "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Aliquam vitae metus eget sapien vulputate fermentum et ac tellus. Donec erat quam, bibendum eget consequat sit amet, egestas ut urna. Praesent felis nunc, fermentum id turpis vitae, congue sagittis erat. Duis iaculis urna non volutpat auctor. Maecenas a sapien varius, auctor ipsum quis, interdum elit. Nunc luctus ornare orci, ac porttitor magna ornare id. Vivamus pretium cursus velit eget vulputate. Vestibulum tellus sem, fringilla non pharetra ac, egestas a nisl. Nunc vitae lectus orci. Lorem ipsum dolor sit amet, consectetur adipiscing elit. Donec mattis, ex ut porta ultricies, nisl arcu vehicula magna, eu laoreet elit elit auctor leo. Mauris eu blandit nunc. Proin hendrerit efficitur mattis. Proin blandit faucibus erat eu dictum. Nunc nec tortor pretium, auctor urna at, ullamcorper enim.\r\n\r\nVestibulum ante ipsum primis in faucibus orci luctus et ultrices posuere cubilia curae; Etiam semper, augue et gravida maximus, tellus tellus tempus sem, hendrerit in.";
            var cipherMode = CipherMode.CBC;
            var server = BuildTestServer(clientName: "firstServer");
            _firstServer = server;
            var client = CreateClientConnection(server);
            var clientPassword = "firstClientPassword";
            var clientLoginResponse = await LoginClientAsync(client.httpClient, clientPassword);

            // Act
            var response = await client.httpClient.GetAsync("api/exchange/key");
            await using var file1 = new MemoryStream(Encoding.UTF8.GetBytes(message));
            using var content1 = new StreamContent(file1);
            using var formData = new MultipartFormDataContent();
            formData.Add(content1, "files", "name");
            formData.Add(new StringContent(cipherMode.ToString()), "mode");

            response = await client.httpClient.PostAsync("api/exchange/packageFile", formData);
            var packages = await response.Content.ReadFromJsonAsync<List<Package>>();

            // Assert
            packages.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task DecryptingPackagedText_WhenSessionKeyGenerated_ShouldReturnOriginalText()
        {
            // Arrange
            var message = "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Aliquam vitae metus eget sapien vulputate fermentum et ac tellus. Donec erat quam, bibendum eget consequat sit amet, egestas ut urna. Praesent felis nunc, fermentum id turpis vitae, congue sagittis erat. Duis iaculis urna non volutpat auctor. Maecenas a sapien varius, auctor ipsum quis, interdum elit. Nunc luctus ornare orci, ac porttitor magna ornare id. Vivamus pretium cursus velit eget vulputate. Vestibulum tellus sem, fringilla non pharetra ac, egestas a nisl. Nunc vitae lectus orci. Lorem ipsum dolor sit amet, consectetur adipiscing elit. Donec mattis, ex ut porta ultricies, nisl arcu vehicula magna, eu laoreet elit elit auctor leo. Mauris eu blandit nunc. Proin hendrerit efficitur mattis. Proin blandit faucibus erat eu dictum. Nunc nec tortor pretium, auctor urna at, ullamcorper enim.\r\n\r\nVestibulum ante ipsum primis in faucibus orci luctus et ultrices posuere cubilia curae; Etiam semper, augue et gravida maximus, tellus tellus tempus sem, hendrerit in.";
            var server = BuildTestServer(clientName: "firstServer");
            _firstServer = server;
            var client = CreateClientConnection(server);
            var clientPassword = "firstClientPassword";
            var clientResetEvent = new ManualResetEvent(false);
            var clientLoginResponse = await LoginClientAsync(client.httpClient, clientPassword);

            // Act
            var response = await client.httpClient.GetAsync("api/exchange/key");
            response = await client.httpClient.PostAsJsonAsync<PackageTextRequest>("api/exchange/packageText", new PackageTextRequest
            {
                Text = message
            });
            var packages = await response.Content.ReadFromJsonAsync<List<Package>>();

            var encryptor = server.Services.GetRequiredService<IEncryptorService>();
            encryptor.AddPackageIds(packages.Select(p => p.Id).ToList());
            packages.ForEach(p => encryptor.AddPackage(p));
            var msg = await encryptor.GetMessageFromPackagesAsync();

            // Assert
            msg.Should().Be(message);
        }

        [Fact]
        public async Task GeneratingRSAKeys_WhenLoggingIn_KeysShouldNotBeInPlainText()
        {
            // Arrange
            var message = "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Aliquam vitae metus eget sapien vulputate fermentum et ac tellus. Donec erat quam, bibendum eget consequat sit amet, egestas ut urna. Praesent felis nunc, fermentum id turpis vitae, congue sagittis erat. Duis iaculis urna non volutpat auctor. Maecenas a sapien varius, auctor ipsum quis, interdum elit. Nunc luctus ornare orci, ac porttitor magna ornare id. Vivamus pretium cursus velit eget vulputate. Vestibulum tellus sem, fringilla non pharetra ac, egestas a nisl. Nunc vitae lectus orci. Lorem ipsum dolor sit amet, consectetur adipiscing elit. Donec mattis, ex ut porta ultricies, nisl arcu vehicula magna, eu laoreet elit elit auctor leo. Mauris eu blandit nunc. Proin hendrerit efficitur mattis. Proin blandit faucibus erat eu dictum. Nunc nec tortor pretium, auctor urna at, ullamcorper enim.\r\n\r\nVestibulum ante ipsum primis in faucibus orci luctus et ultrices posuere cubilia curae; Etiam semper, augue et gravida maximus, tellus tellus tempus sem, hendrerit in.";
            var server = BuildTestServer(clientName: "firstServer");
            var encryptor = server.Services.GetRequiredService<IEncryptorService>();
            var keyStore = server.Services.GetRequiredService<IKeyStore>();
            _firstServer = server;
            var client = CreateClientConnection(server);
            var clientPassword = "firstClientPassword";
            var hashedPassword = encryptor.HashPassword(clientPassword);
            var clientLoginResponse = await LoginClientAsync(client.httpClient, clientPassword);

            // Act
            var publicKey = encryptor.PublicKey;
            var privateKey = encryptor.PrivateKey;
            var encryptedKeys = keyStore.GetEncryptedKeys(hashedPassword.hashedPassword);

            // Assert
            Convert.FromBase64String(encryptedKeys.PublicKey).Should().NotEqual(publicKey);
            Convert.FromBase64String(encryptedKeys.PrivateKey).Should().NotEqual(privateKey);
        }

        [Theory]
        [InlineData(CipherMode.ECB)]
        [InlineData(CipherMode.CBC)]
        [InlineData(CipherMode.CFB)]
        [InlineData(CipherMode.OFB)]
        public async Task SendingText_WhenClientsExchangedKeys_SecondClientShouldReceiveCorrectMessage(CipherMode cipherMode)
        {
            // Arrange
            var message = "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Aliquam vitae metus eget sapien vulputate fermentum et ac tellus. Donec erat quam, bibendum eget consequat sit amet, egestas ut urna. Praesent felis nunc, fermentum id turpis vitae, congue sagittis erat. Duis iaculis urna non volutpat auctor. Maecenas a sapien varius, auctor ipsum quis, interdum elit. Nunc luctus ornare orci, ac porttitor magna ornare id. Vivamus pretium cursus velit eget vulputate. Vestibulum tellus sem, fringilla non pharetra ac, egestas a nisl. Nunc vitae lectus orci. Lorem ipsum dolor sit amet, consectetur adipiscing elit. Donec mattis, ex ut porta ultricies, nisl arcu vehicula magna, eu laoreet elit elit auctor leo. Mauris eu blandit nunc. Proin hendrerit efficitur mattis. Proin blandit faucibus erat eu dictum. Nunc nec tortor pretium, auctor urna at, ullamcorper enim.\r\n\r\nVestibulum ante ipsum primis in faucibus orci luctus et ultrices posuere cubilia curae; Etiam semper, augue et gravida maximus, tellus tellus tempus sem, hendrerit in.";
            message = Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(message).Take(1023).ToArray());
            var receivedMessage = string.Empty;
            var firstServer = BuildTestServer(clientName: "firstServer");
            _firstServer = firstServer;
            var secondServer = BuildTestServer(builder =>
            {
                builder.UseUrls("http://localhost:24555", "https://localhost:24556");
            }, "secondServer");
            _secondServer = secondServer;

            var firstClient = await SetupConnectionsForBothServersAsync(firstServer, secondServer);
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

            var secondClient = await SetupConnectionsForBothServersAsync(secondServer, firstServer);
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
        [InlineData(CipherMode.ECB)]
        [InlineData(CipherMode.CBC)]
        [InlineData(CipherMode.CFB)]
        [InlineData(CipherMode.OFB)]
        public async Task SendingText_WhenClientsExchangedKeys_FirstClientShouldReceiveCorrectMessage(CipherMode cipherMode)
        {
            // Arrange
            var message = "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Aliquam vitae metus eget sapien vulputate fermentum et ac tellus. Donec erat quam, bibendum eget consequat sit amet, egestas ut urna. Praesent felis nunc, fermentum id turpis vitae, congue sagittis erat. Duis iaculis urna non volutpat auctor. Maecenas a sapien varius, auctor ipsum quis, interdum elit. Nunc luctus ornare orci, ac porttitor magna ornare id. Vivamus pretium cursus velit eget vulputate. Vestibulum tellus sem, fringilla non pharetra ac, egestas a nisl. Nunc vitae lectus orci. Lorem ipsum dolor sit amet, consectetur adipiscing elit. Donec mattis, ex ut porta ultricies, nisl arcu vehicula magna, eu laoreet elit elit auctor leo. Mauris eu blandit nunc. Proin hendrerit efficitur mattis. Proin blandit faucibus erat eu dictum. Nunc nec tortor pretium, auctor urna at, ullamcorper enim.\r\n\r\nVestibulum ante ipsum primis in faucibus orci luctus et ultrices posuere cubilia curae; Etiam semper, augue et gravida maximus, tellus tellus tempus sem, hendrerit in.";
            message = Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(message).Take(1023).ToArray());
            var receivedMessage = string.Empty;
            var firstServer = BuildTestServer(clientName: "firstServer");
            _firstServer = firstServer;
            var secondServer = BuildTestServer(builder =>
            {
                builder.UseUrls("http://localhost:24555", "https://localhost:24556");
            }, "secondServer");
            _secondServer = secondServer;

            var firstClient = await SetupConnectionsForBothServersAsync(firstServer, secondServer);
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
            firstClient.hostConnection.On<string>("ReceiveStopSendingMessage", (msg) =>
            {
                receivedMessage = msg;
                firstClientResetEvent.Set();
            });

            var secondClient = await SetupConnectionsForBothServersAsync(secondServer, firstServer);
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

            response = await secondClient.httpClient.PostAsJsonAsync<PackageTextRequest>("api/exchange/packageText", new PackageTextRequest
            {
                Text = message,
                Mode = cipherMode
            });
            var packages = await response.Content.ReadFromJsonAsync<List<Package>>();

            await secondClient.clientConnection.InvokeAsync(nameof(MessageHub.StartSendingMessage), packages.Select(p => p.Id).ToList(),
                packages.Count, cipherMode, 128);
            foreach (var package in packages)
            {
                await secondClient.clientConnection.InvokeAsync(nameof(MessageHub.SendPackage), package);
            }
            await secondClient.clientConnection.InvokeAsync(nameof(MessageHub.StopSendingMessage));
            firstClientResetEvent.WaitOne();

            // Assert
            receivedMessage.Should().Be(message);
        }

        [Theory]
        [InlineData(CipherMode.ECB)]
        [InlineData(CipherMode.CBC)]
        [InlineData(CipherMode.CFB)]
        [InlineData(CipherMode.OFB)]
        public async Task SendingText_WhenFirstClientNotAuthenticatedAndClientsExchangedKeys_SecondClientShouldReceiveDummyMessage(CipherMode cipherMode)
        {
            // Arrange
            var message = "T";
            message = Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(message).Take(1024).ToArray());
            var receivedMessage = string.Empty;
            var firstServer = BuildTestServer(clientName: "firstServer");
            _firstServer = firstServer;
            var secondServer = BuildTestServer(builder =>
            {
                builder.UseUrls("http://localhost:24555", "https://localhost:24556");
            }, "secondServer");
            _secondServer = secondServer;

            var dummyService = firstServer.Services.GetService<IDummyDataService>();
            var firstClient = await SetupConnectionsForBothServersAsync(firstServer, secondServer);
            var firstClientPassword = "firstClientPassword";
            var firstClientResetEvent = new ManualResetEvent(false);
            var firstClientLoginResponse = await LoginClientAsync(firstClient.httpClient, firstClientPassword);
            firstClientLoginResponse = await LoginClientAsync(firstClient.httpClient, firstClientPassword + "T");
            firstClient.hostConnection.On<bool>("ReceiveSendReceiverPublicKey", (b) =>
            {
                firstClientResetEvent.Set();
            });
            firstClient.clientConnection.On("ReceiveSendEncryptedSessionKey", () =>
            {
                firstClientResetEvent.Set();
            });

            var secondClient = await SetupConnectionsForBothServersAsync(secondServer, firstServer);
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
            receivedMessage.Should().Be(dummyService.GetStringDummyData());
        }

        [Theory]
        [InlineData(CipherMode.ECB)]
        [InlineData(CipherMode.CBC)]
        [InlineData(CipherMode.CFB)]
        [InlineData(CipherMode.OFB)]
        public async Task SendingText_WhenSecondClientNotAuthenticatedAndClientsExchangedKeys_SecondClientShouldReceiveDummyMessage(CipherMode cipherMode)
        {
            // Arrange
            var message = "T";
            message = Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(message).Take(1024).ToArray());
            var receivedMessage = string.Empty;
            var firstServer = BuildTestServer(clientName: "firstServer");
            _firstServer = firstServer;
            var secondServer = BuildTestServer(builder =>
            {
                builder.UseUrls("http://localhost:24555", "https://localhost:24556");
            }, "secondServer");
            _secondServer = secondServer;

            var dummyService = firstServer.Services.GetService<IDummyDataService>();
            var firstClient = await SetupConnectionsForBothServersAsync(firstServer, secondServer);
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

            var secondClient = await SetupConnectionsForBothServersAsync(secondServer, firstServer);
            var secondClientPassword = "secondClientPassword";
            var secondClientResetEvent = new ManualResetEvent(false);
            var secondClientLoginResponse = await LoginClientAsync(secondClient.httpClient, secondClientPassword);
            secondClientLoginResponse = await LoginClientAsync(secondClient.httpClient, secondClientPassword + "T");
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
                Text = message
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
            receivedMessage.Should().Be(dummyService.GetStringDummyData());
        }

        [Theory]
        [InlineData(CipherMode.ECB)]
        [InlineData(CipherMode.CBC)]
        [InlineData(CipherMode.CFB)]
        [InlineData(CipherMode.OFB)]
        public async Task SendingFile_WhenClientsExchangedKeys_SecondClientShouldReceiveCorrectFile(CipherMode cipherMode)
        {
            // Arrange
            var message = "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Aliquam vitae metus eget sapien vulputate fermentum et ac tellus. Donec erat quam, bibendum eget consequat sit amet, egestas ut urna. Praesent felis nunc, fermentum id turpis vitae, congue sagittis erat. Duis iaculis urna non volutpat auctor. Maecenas a sapien varius, auctor ipsum quis, interdum elit. Nunc luctus ornare orci, ac porttitor magna ornare id. Vivamus pretium cursus velit eget vulputate. Vestibulum tellus sem, fringilla non pharetra ac, egestas a nisl. Nunc vitae lectus orci. Lorem ipsum dolor sit amet, consectetur adipiscing elit. Donec mattis, ex ut porta ultricies, nisl arcu vehicula magna, eu laoreet elit elit auctor leo. Mauris eu blandit nunc. Proin hendrerit efficitur mattis. Proin blandit faucibus erat eu dictum. Nunc nec tortor pretium, auctor urna at, ullamcorper enim.\r\n\r\nVestibulum ante ipsum primis in faucibus orci luctus et ultrices posuere cubilia curae; Etiam semper, augue et gravida maximus, tellus tellus tempus sem, hendrerit in.";
            var b = Encoding.UTF8.GetBytes(message);
            var fileName = "testFile.txt";
            var receivedFileName = string.Empty;
            var firstServer = BuildTestServer(clientName: "firstServer");
            _firstServer = firstServer;
            var secondServer = BuildTestServer(builder =>
            {
                builder.UseUrls("http://localhost:24555", "https://localhost:24556");
            }, "secondServer");
            _secondServer = secondServer;

            var fileManager = secondServer.Services.GetRequiredService<IFileManager>();
            var firstClient = await SetupConnectionsForBothServersAsync(firstServer, secondServer);
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

            var secondClient = await SetupConnectionsForBothServersAsync(secondServer, firstServer);
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
            secondClient.hostConnection.On<string>("ReceiveStopSendingFile", (fileName) =>
            {
                receivedFileName = fileName;
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

            await using var file1 = new MemoryStream(Encoding.UTF8.GetBytes(message));
            using var content1 = new StreamContent(file1);
            using var formData = new MultipartFormDataContent();
            formData.Add(content1, "files", "name");
            formData.Add(new StringContent(cipherMode.ToString()), "mode");
            response = await firstClient.httpClient.PostAsync("api/exchange/packageFile", formData);
            var packages = await response.Content.ReadFromJsonAsync<List<Package>>();
            await firstClient.clientConnection.InvokeAsync(nameof(MessageHub.StartSendingFile), packages.Select(p => p.Id).ToList(),
                packages.Count, cipherMode, 128);
            foreach (var package in packages)
            {
                await firstClient.clientConnection.InvokeAsync(nameof(MessageHub.SendPackage), package);
            }

            await firstClient.clientConnection.InvokeAsync(nameof(MessageHub.StopSendingFile), fileName);
            secondClientResetEvent.WaitOne();

            // Assert
            var receivedBytes = fileManager.GetFile(receivedFileName);
            receivedFileName.Should().Be(fileName);
            receivedBytes.Should().Equal(Encoding.UTF8.GetBytes(message));

        }

        [Theory]
        [InlineData(CipherMode.ECB)]
        [InlineData(CipherMode.CBC)]
        [InlineData(CipherMode.CFB)]
        [InlineData(CipherMode.OFB)]
        public async Task SendingFile_WhenFirstClientNotAuthenticatedAndClientsExchangedKeys_SecondClientShouldReceiveDummyFile(CipherMode cipherMode)
        {
            // Arrange
            var message = "Test";
            var b = Encoding.UTF8.GetBytes(message);
            var fileName = "testFile.txt";
            var receivedFileName = string.Empty;
            var firstServer = BuildTestServer(clientName: "firstServer");
            _firstServer = firstServer;
            var secondServer = BuildTestServer(builder =>
            {
                builder.UseUrls("http://localhost:24555", "https://localhost:24556");
            }, "secondServer");
            _secondServer = secondServer;

            var fileManager = secondServer.Services.GetRequiredService<IFileManager>();
            var dummyService = firstServer.Services.GetService<IDummyDataService>();
            var firstClient = await SetupConnectionsForBothServersAsync(firstServer, secondServer);
            var firstClientPassword = "firstClientPassword";
            var firstClientResetEvent = new ManualResetEvent(false);
            var firstClientLoginResponse = await LoginClientAsync(firstClient.httpClient, firstClientPassword);
            firstClientLoginResponse = await LoginClientAsync(firstClient.httpClient, firstClientPassword + "T");
            firstClient.hostConnection.On<bool>("ReceiveSendReceiverPublicKey", (b) =>
            {
                firstClientResetEvent.Set();
            });
            firstClient.clientConnection.On("ReceiveSendEncryptedSessionKey", () =>
            {
                firstClientResetEvent.Set();
            });

            var secondClient = await SetupConnectionsForBothServersAsync(secondServer, firstServer);
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
            secondClient.hostConnection.On<string>("ReceiveStopSendingFile", (fileName) =>
            {
                receivedFileName = fileName;
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

            await using var file1 = new MemoryStream(Encoding.UTF8.GetBytes(message));
            using var content1 = new StreamContent(file1);
            using var formData = new MultipartFormDataContent();
            formData.Add(content1, "files", "name");
            formData.Add(new StringContent(cipherMode.ToString()), "mode");
            response = await firstClient.httpClient.PostAsync("api/exchange/packageFile", formData);
            var packages = await response.Content.ReadFromJsonAsync<List<Package>>();
            await firstClient.clientConnection.InvokeAsync(nameof(MessageHub.StartSendingFile), packages.Select(p => p.Id).ToList(),
                packages.Count, cipherMode, 128);
            foreach (var package in packages)
            {
                await firstClient.clientConnection.InvokeAsync(nameof(MessageHub.SendPackage), package);
            }

            await firstClient.clientConnection.InvokeAsync(nameof(MessageHub.StopSendingFile), fileName);
            secondClientResetEvent.WaitOne();

            // Assert
            var receivedBytes = fileManager.GetFile(receivedFileName);
            receivedFileName.Should().Be(fileName);
            receivedBytes.Should().Equal(dummyService.GetBytesDummyData());

        }

        [Theory]
        [InlineData(CipherMode.ECB)]
        [InlineData(CipherMode.CBC)]
        [InlineData(CipherMode.CFB)]
        [InlineData(CipherMode.OFB)]
        public async Task SendingFile_WhenSecondClientNotAuthenticatedAndClientsExchangedKeys_SecondClientShouldReceiveDummyFile(CipherMode cipherMode)
        {
            // Arrange
            var message = "Test";
            var fileName = "testFile.txt";
            var receivedFileName = string.Empty;
            var firstServer = BuildTestServer(clientName: "firstServer");
            _firstServer = firstServer;
            var secondServer = BuildTestServer(builder =>
            {
                builder.UseUrls("http://localhost:24555", "https://localhost:24556");
            }, "secondServer");
            _secondServer = secondServer;

            var fileManager = secondServer.Services.GetRequiredService<IFileManager>();
            var dummyService = firstServer.Services.GetService<IDummyDataService>();
            var firstClient = await SetupConnectionsForBothServersAsync(firstServer, secondServer);
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

            var secondClient = await SetupConnectionsForBothServersAsync(secondServer, firstServer);
            var secondClientPassword = "secondClientPassword";
            var secondClientResetEvent = new ManualResetEvent(false);
            var secondClientLoginResponse = await LoginClientAsync(secondClient.httpClient, secondClientPassword);
            secondClientLoginResponse = await LoginClientAsync(secondClient.httpClient, secondClientPassword + "T");
            secondClient.clientConnection.On<bool>("ReceiveSendReceiverPublicKey", (b) =>
            {
                secondClientResetEvent.Set();
            });
            secondClient.hostConnection.On("ReceiveSendEncryptedSessionKey", () =>
            {
                secondClientResetEvent.Set();
            });
            secondClient.hostConnection.On<string>("ReceiveStopSendingFile", (fileName) =>
            {
                receivedFileName = fileName;
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

            await using var file1 = new MemoryStream(Encoding.UTF8.GetBytes(message));
            using var content1 = new StreamContent(file1);
            using var formData = new MultipartFormDataContent();
            formData.Add(content1, "files", "name");
            formData.Add(new StringContent(cipherMode.ToString()), "mode");
            response = await firstClient.httpClient.PostAsync("api/exchange/packageFile", formData);
            var packages = await response.Content.ReadFromJsonAsync<List<Package>>();
            await firstClient.clientConnection.InvokeAsync(nameof(MessageHub.StartSendingFile), packages.Select(p => p.Id).ToList(),
                packages.Count, cipherMode, 128);
            foreach (var package in packages)
            {
                await firstClient.clientConnection.InvokeAsync(nameof(MessageHub.SendPackage), package);
            }

            await firstClient.clientConnection.InvokeAsync(nameof(MessageHub.StopSendingFile), fileName);
            secondClientResetEvent.WaitOne();

            // Assert
            var receivedBytes = fileManager.GetFile(receivedFileName);
            receivedFileName.Should().Be(fileName);
            receivedBytes.Should().Equal(dummyService.GetBytesDummyData());

        }

        [Theory]
        [InlineData(CipherMode.ECB)]
        [InlineData(CipherMode.CBC)]
        [InlineData(CipherMode.CFB)]
        [InlineData(CipherMode.OFB)]
        public async Task Sending1MBFile_WhenClientsExchangedKeys_SecondClientShouldReceiveCorrectFile(CipherMode cipherMode)
        {
            // Arrange
            var originalBytes = new byte[1024 * 1024];
            new Random().NextBytes(originalBytes);
            var fileName = "testFile.txt";
            var receivedFileName = string.Empty;
            var firstServer = BuildTestServer(clientName: "firstServer");
            _firstServer = firstServer;
            var secondServer = BuildTestServer(builder =>
            {
                builder.UseUrls("http://localhost:24555", "https://localhost:24556");
            }, "secondServer");
            _secondServer = secondServer;

            var fileManager = secondServer.Services.GetRequiredService<IFileManager>();
            var firstClient = await SetupConnectionsForBothServersAsync(firstServer, secondServer);
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

            var secondClient = await SetupConnectionsForBothServersAsync(secondServer, firstServer);
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
            secondClient.hostConnection.On<string>("ReceiveStopSendingFile", (fileName) =>
            {
                receivedFileName = fileName;
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
            using var formData = new MultipartFormDataContent();
            formData.Add(content1, "files", "name");
            formData.Add(new StringContent(cipherMode.ToString()), "mode");

            response = await firstClient.httpClient.PostAsync("api/exchange/packageFile", formData);
            var packages = await response.Content.ReadFromJsonAsync<List<Package>>();
            await firstClient.clientConnection.InvokeAsync(nameof(MessageHub.StartSendingFile), packages.Select(p => p.Id).ToList(),
            packages.Count, cipherMode, 128);
            foreach (var package in packages)
            {
                await firstClient.clientConnection.InvokeAsync(nameof(MessageHub.SendPackage), package);
            }

            await firstClient.clientConnection.InvokeAsync(nameof(MessageHub.StopSendingFile), fileName);
            secondClientResetEvent.WaitOne();

            // Assert
            var receivedBytes = fileManager.GetFile(receivedFileName);
            receivedFileName.Should().Be(fileName);
            receivedBytes.Should().Equal(originalBytes);

        }

        [Theory]
        [InlineData(CipherMode.ECB)]
        [InlineData(CipherMode.CBC)]
        [InlineData(CipherMode.CFB)]
        [InlineData(CipherMode.OFB)]
        public async Task Sending1MBFile_WhenClientsExchangedKeys_FirstClientShouldReceiveCorrectFile(CipherMode cipherMode)
        {
            // Arrange
            var originalBytes = new byte[1024 * 1024];
            new Random().NextBytes(originalBytes);
            var fileName = "testFile.txt";
            var receivedFileName = string.Empty;
            var firstServer = BuildTestServer(clientName: "firstServer");
            _firstServer = firstServer;
            var secondServer = BuildTestServer(builder =>
            {
                builder.UseUrls("http://localhost:24555", "https://localhost:24556");
            }, "secondServer");
            _secondServer = secondServer;

            var fileManager = firstServer.Services.GetRequiredService<IFileManager>();
            var firstClient = await SetupConnectionsForBothServersAsync(firstServer, secondServer);
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

            var secondClient = await SetupConnectionsForBothServersAsync(secondServer, firstServer);
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
            using var formData = new MultipartFormDataContent();
            formData.Add(content1, "files", "name");
            formData.Add(new StringContent(cipherMode.ToString()), "mode");

            response = await secondClient.httpClient.PostAsync("api/exchange/packageFile", formData);
            var packages = await response.Content.ReadFromJsonAsync<List<Package>>();
            await secondClient.clientConnection.InvokeAsync(nameof(MessageHub.StartSendingFile), packages.Select(p => p.Id).ToList(),
            packages.Count, cipherMode, 128);
            foreach (var package in packages)
            {
                await secondClient.clientConnection.InvokeAsync(nameof(MessageHub.SendPackage), package);
            }

            await secondClient.clientConnection.InvokeAsync(nameof(MessageHub.StopSendingFile), fileName);
            firstClientResetEvent.WaitOne();

            // Assert
            var receivedBytes = fileManager.GetFile(receivedFileName);
            receivedFileName.Should().Be(fileName);
            receivedBytes.Should().Equal(originalBytes);

        }

        [Theory]
        [InlineData(CipherMode.ECB)]
        [InlineData(CipherMode.CBC)]
        [InlineData(CipherMode.CFB)]
        [InlineData(CipherMode.OFB)]
        public async Task Sending10MBFile_WhenClientsExchangedKeys_SecondClientShouldReceiveCorrectFile(CipherMode cipherMode)
        {
            // Arrange
            var originalBytes = new byte[1024 * 1024 * 10];
            new Random().NextBytes(originalBytes);
            var fileName = "testFile.txt";
            var receivedFileName = string.Empty;
            var firstServer = BuildTestServer(clientName: "firstServer");
            _firstServer = firstServer;
            var secondServer = BuildTestServer(builder =>
            {
                builder.UseUrls("http://localhost:24555", "https://localhost:24556");
            }, "secondServer");
            _secondServer = secondServer;

            var fileManager = secondServer.Services.GetRequiredService<IFileManager>();
            var firstClient = await SetupConnectionsForBothServersAsync(firstServer, secondServer);
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

            var secondClient = await SetupConnectionsForBothServersAsync(secondServer, firstServer);
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
            secondClient.hostConnection.On<string>("ReceiveStopSendingFile", (fileName) =>
            {
                receivedFileName = fileName;
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
            using var formData = new MultipartFormDataContent();
            formData.Add(content1, "files", "name");
            formData.Add(new StringContent(cipherMode.ToString()), "mode");
            response = await firstClient.httpClient.PostAsync("api/exchange/packageFile", formData);
            var packages = await response.Content.ReadFromJsonAsync<List<Package>>();
            await firstClient.clientConnection.InvokeAsync(nameof(MessageHub.StartSendingFile), packages.Select(p => p.Id).ToList(),
            packages.Count, cipherMode, 128);
            for (int i = 0; i < packages.Count; ++i)
            {
                await firstClient.clientConnection.InvokeAsync(nameof(MessageHub.SendPackage), packages[i]);
            }

            await firstClient.clientConnection.InvokeAsync(nameof(MessageHub.StopSendingFile), fileName);
            secondClientResetEvent.WaitOne();

            // Assert
            var receivedBytes = fileManager.GetFile(receivedFileName);
            receivedFileName.Should().Be(fileName);
            receivedBytes.Should().Equal(originalBytes);


        }

        [Theory]
        [InlineData(CipherMode.ECB)]
        [InlineData(CipherMode.CBC)]
        [InlineData(CipherMode.CFB)]
        [InlineData(CipherMode.OFB)]
        public async Task Sending10MBFile_WhenClientsExchangedKeys_FirstClientShouldReceiveCorrectFile(CipherMode cipherMode)
        {
            // Arrange
            var originalBytes = new byte[1024 * 1024 * 10];
            new Random().NextBytes(originalBytes);
            var fileName = "testFile.txt";
            var receivedFileName = string.Empty;
            var firstServer = BuildTestServer(clientName: "firstServer");
            _firstServer = firstServer;
            var secondServer = BuildTestServer(builder =>
            {
                builder.UseUrls("http://localhost:24555", "https://localhost:24556");
            }, "secondServer");
            _secondServer = secondServer;

            var fileManager = firstServer.Services.GetRequiredService<IFileManager>();
            var firstClient = await SetupConnectionsForBothServersAsync(firstServer, secondServer);
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

            var secondClient = await SetupConnectionsForBothServersAsync(secondServer, firstServer);
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
            using var formData = new MultipartFormDataContent();
            formData.Add(content1, "files", "name");
            formData.Add(new StringContent(cipherMode.ToString()), "mode");

            response = await secondClient.httpClient.PostAsync("api/exchange/packageFile", formData);
            var packages = await response.Content.ReadFromJsonAsync<List<Package>>();
            await secondClient.clientConnection.InvokeAsync(nameof(MessageHub.StartSendingFile), packages.Select(p => p.Id).ToList(),
            packages.Count, cipherMode, 128);
            foreach (var package in packages)
            {
                await secondClient.clientConnection.InvokeAsync(nameof(MessageHub.SendPackage), package);
            }

            await secondClient.clientConnection.InvokeAsync(nameof(MessageHub.StopSendingFile), fileName);
            firstClientResetEvent.WaitOne();

            // Assert
            var receivedBytes = fileManager.GetFile(receivedFileName);
            receivedFileName.Should().Be(fileName);
            receivedBytes.Should().Equal(originalBytes);

        }

        [Theory]
        [InlineData(CipherMode.ECB)]
        [InlineData(CipherMode.CBC)]
        [InlineData(CipherMode.CFB)]
        [InlineData(CipherMode.OFB)]
        public async Task Sending50MBFile_WhenClientsExchangedKeys_SecondClientShouldReceiveCorrectFile(CipherMode cipherMode)
        {
            // Arrange
            var originalBytes = new byte[1024 * 1024 * 50];
            new Random().NextBytes(originalBytes);
            var fileName = "testFile.txt";
            var receivedFileName = string.Empty;
            var firstServer = BuildTestServer(clientName: "firstServer");
            _firstServer = firstServer;
            var secondServer = BuildTestServer(builder =>
            {
                builder.UseUrls("http://localhost:24555", "https://localhost:24556");
            }, "secondServer");
            _secondServer = secondServer;

            var fileManager = secondServer.Services.GetRequiredService<IFileManager>();
            var firstClient = await SetupConnectionsForBothServersAsync(firstServer, secondServer);
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

            var secondClient = await SetupConnectionsForBothServersAsync(secondServer, firstServer);
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
            secondClient.hostConnection.On<string>("ReceiveStopSendingFile", (fileName) =>
            {
                receivedFileName = fileName;
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
            using var formData = new MultipartFormDataContent();
            formData.Add(content1, "files", "name");
            formData.Add(new StringContent(cipherMode.ToString()), "mode");
            response = await firstClient.httpClient.PostAsync("api/exchange/packageFile", formData);
            var packages = await response.Content.ReadFromJsonAsync<List<Package>>();
            await firstClient.clientConnection.InvokeAsync(nameof(MessageHub.StartSendingFile), packages.Select(p => p.Id).ToList(),
            packages.Count, cipherMode, 128);
            for (int i = 0; i < packages.Count; ++i)
            {
                await firstClient.clientConnection.InvokeAsync(nameof(MessageHub.SendPackage), packages[i]);
            }

            await firstClient.clientConnection.InvokeAsync(nameof(MessageHub.StopSendingFile), fileName);
            secondClientResetEvent.WaitOne();

            // Assert
            var receivedBytes = fileManager.GetFile(receivedFileName);
            receivedFileName.Should().Be(fileName);
            receivedBytes.Should().Equal(originalBytes);


        }

        [Theory]
        [InlineData(CipherMode.ECB)]
        [InlineData(CipherMode.CBC)]
        [InlineData(CipherMode.CFB)]
        [InlineData(CipherMode.OFB)]
        public async Task Sending50MBFile_WhenClientsExchangedKeys_FirstClientShouldReceiveCorrectFile(CipherMode cipherMode)
        {
            // Arrange
            var originalBytes = new byte[1024 * 1024 * 50];
            new Random().NextBytes(originalBytes);
            var fileName = "testFile.txt";
            var receivedFileName = string.Empty;
            var firstServer = BuildTestServer(clientName: "firstServer");
            _firstServer = firstServer;
            var secondServer = BuildTestServer(builder =>
            {
                builder.UseUrls("http://localhost:24555", "https://localhost:24556");
            }, "secondServer");
            _secondServer = secondServer;

            var fileManager = firstServer.Services.GetRequiredService<IFileManager>();
            var firstClient = await SetupConnectionsForBothServersAsync(firstServer, secondServer);
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

            var secondClient = await SetupConnectionsForBothServersAsync(secondServer, firstServer);
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
            using var formData = new MultipartFormDataContent();
            formData.Add(content1, "files", "name");
            formData.Add(new StringContent(cipherMode.ToString()), "mode");

            response = await secondClient.httpClient.PostAsync("api/exchange/packageFile", formData);
            var packages = await response.Content.ReadFromJsonAsync<List<Package>>();
            await secondClient.clientConnection.InvokeAsync(nameof(MessageHub.StartSendingFile), packages.Select(p => p.Id).ToList(),
            packages.Count, cipherMode, 128);
            foreach (var package in packages)
            {
                await secondClient.clientConnection.InvokeAsync(nameof(MessageHub.SendPackage), package);
            }

            await secondClient.clientConnection.InvokeAsync(nameof(MessageHub.StopSendingFile), fileName);
            firstClientResetEvent.WaitOne();

            // Assert
            var receivedBytes = fileManager.GetFile(receivedFileName);
            receivedFileName.Should().Be(fileName);
            receivedBytes.Should().Equal(originalBytes);
        }

        [Theory]
        [InlineData(CipherMode.ECB)]
        [InlineData(CipherMode.CBC)]
        [InlineData(CipherMode.CFB)]
        [InlineData(CipherMode.OFB)]
        public async Task MessagesAndFilesExchange_WhenClientsExchangedKeys_BothClientsShouldReceiveMessagesAndFiles(CipherMode cipherMode)
        {
            // Arrange
            var receivedFileName = string.Empty;
            var fileName = "testFile";
            var message =
                "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Aliquam vitae metus eget sapien vulputate fermentum et ac tellus. Donec erat quam, bibendum eget consequat sit amet, egestas ut urna. Praesent felis nunc, fermentum id turpis vitae, congue sagittis erat. Duis iaculis urna non volutpat auctor. Maecenas a sapien varius, auctor ipsum quis, interdum elit. Nunc luctus ornare orci, ac porttitor magna ornare id. Vivamus pretium cursus velit eget vulputate. Vestibulum tellus sem, fringilla non pharetra ac, egestas a nisl. Nunc vitae lectus orci. Lorem ipsum dolor sit amet, consectetur adipiscing elit. Donec mattis, ex ut porta ultricies, nisl arcu vehicula magna, eu laoreet elit elit auctor leo. Mauris eu blandit nunc. Proin hendrerit efficitur mattis. Proin blandit faucibus erat eu dictum. Nunc nec tortor pretium, auctor urna at, ullamcorper enim.\r\n\r\nVestibulum ante ipsum primis in faucibus orci luctus et ultrices posuere cubilia curae; Etiam semper, augue et gravida maximus, tellus tellus tempus sem, hendrerit in.";
            message = Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(message).Take(1023).ToArray());
            var chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var random = new Random();
            Func<int, string> generateMessage = (length) =>
            {
                return new string(Enumerable.Repeat(chars, length)
                    .Select(s => s[random.Next(s.Length)]).ToArray());
            };
            var messages = new List<string>
            {
                Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(message).Take(0).ToArray()),
                Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(message).Take(1).ToArray()),
                Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(message).Take(10).ToArray()),
                Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(message).Take(15).ToArray()),
                Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(message).Take(23).ToArray()),
                Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(message).Take(54).ToArray()),
                Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(message).Take(72).ToArray()),
                Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(message).Take(80).ToArray()),
                Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(message).Take(111).ToArray()),
                Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(message).Take(727).ToArray()),
                generateMessage(1024 * 512),
                generateMessage(1024 * 1024),
                generateMessage(1024 * 1024 * 3)
            };

            var files = new List<byte[]>();
            var fileNames = new List<string>();
            var bytes = new byte[0];
            random.NextBytes(bytes);
            files.Add(bytes);
            bytes = new byte[1];
            random.NextBytes(bytes);
            files.Add(bytes);
            bytes = new byte[2];
            random.NextBytes(bytes);
            files.Add(bytes);
            bytes = new byte[15];
            random.NextBytes(bytes);
            files.Add(bytes);
            bytes = new byte[16];
            random.NextBytes(bytes);
            files.Add(bytes);
            bytes = new byte[126];
            random.NextBytes(bytes);
            files.Add(bytes);
            bytes = new byte[512];
            random.NextBytes(bytes);
            files.Add(bytes);
            bytes = new byte[1024];
            random.NextBytes(bytes);
            files.Add(bytes);
            bytes = new byte[1024 * 1024];
            random.NextBytes(bytes);
            files.Add(bytes);
            bytes = new byte[(int)(1024 * 1024 * 1.5)];
            random.NextBytes(bytes);
            files.Add(bytes);

            for (var i = 0; i < files.Count; i++)
            {
                fileNames.Add($"{fileName}{i}.txt");
            }

            var receivedMessage = string.Empty;
            var firstServer = BuildTestServer(clientName: "firstServer");
            _firstServer = firstServer;
            var secondServer =
                BuildTestServer(builder => { builder.UseUrls("http://localhost:24555", "https://localhost:24556"); },
                    "secondServer");
            _secondServer = secondServer;

            var firstReceivedMessages = new List<string>();
            var firstReceivedFiles = new List<byte[]>();
            var firstReceivedFileNames = new List<string>();
            var firstFileManager = firstServer.Services.GetRequiredService<IFileManager>();
            var firstClient = await SetupConnectionsForBothServersAsync(firstServer, secondServer);
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
            var secondFileManager = secondServer.Services.GetRequiredService<IFileManager>();
            var secondClient = await SetupConnectionsForBothServersAsync(secondServer, firstServer);
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
                    await sender.clientConnection.InvokeAsync(nameof(MessageHub.SendPackage), package);
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
                IFileManager receiverFileManager;
                if (i % 2 == 0)
                {
                    sender = firstClient;
                    receiverResetEvent = secondClientResetEvent;
                    receiverReceivedFiles = secondReceivedFiles;
                    receiverReceivedFileNames = secondReceivedFileNames;
                    receiverFileManager = secondFileManager;
                }
                else
                {
                    sender = secondClient;
                    receiverResetEvent = firstClientResetEvent;
                    receiverReceivedFiles = firstReceivedFiles;
                    receiverReceivedFileNames = firstReceivedFileNames;
                    receiverFileManager = firstFileManager;
                }
                await using var file1 = new MemoryStream(file);
                using var content1 = new StreamContent(file1);
                using var formData = new MultipartFormDataContent();
                formData.Add(content1, "files", "name");
                formData.Add(new StringContent(cipherMode.ToString()), "mode");

                response = await sender.httpClient.PostAsync("api/exchange/packageFile", formData);
                var packages = await response.Content.ReadFromJsonAsync<List<Package>>();

                await sender.clientConnection.InvokeAsync(nameof(MessageHub.StartSendingFile),
                    packages.Select(p => p.Id).ToList(),
                    packages.Count, cipherMode, 128);
                foreach (var package in packages)
                {
                    await sender.clientConnection.InvokeAsync(nameof(MessageHub.SendPackage), package);
                }

                await sender.clientConnection.InvokeAsync(nameof(MessageHub.StopSendingFile), fileNames[i]);
                receiverResetEvent.WaitOne();
                receiverResetEvent.Reset();
                receiverReceivedFiles.Add(receiverFileManager.GetFile(receivedFileName));
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

        [Theory]
        [InlineData(CipherMode.ECB)]
        [InlineData(CipherMode.CBC)]
        [InlineData(CipherMode.CFB)]
        [InlineData(CipherMode.OFB)]
        public async Task SendingFile_WhenClientsExchangedKeys_FirstClientShouldReceiveProgressInformation(CipherMode cipherMode)
        {
            // Arrange
            var numberOfPackagesToSend = 0;
            var sentPackages = 0;
            var originalBytes = new byte[1024 * 1024 * 50];
            new Random().NextBytes(originalBytes);
            var fileName = "testFile.txt";
            var receivedFileName = string.Empty;
            var firstServer = BuildTestServer(clientName: "firstServer");
            _firstServer = firstServer;
            var secondServer = BuildTestServer(builder =>
            {
                builder.UseUrls("http://localhost:24555", "https://localhost:24556");
            }, "secondServer");
            _secondServer = secondServer;

            var fileManager = firstServer.Services.GetRequiredService<IFileManager>();
            var firstClient = await SetupConnectionsForBothServersAsync(firstServer, secondServer);
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

            var secondClient = await SetupConnectionsForBothServersAsync(secondServer, firstServer);
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
            secondClient.clientConnection.On<int>("ReceiveProgress", (packageNumber) =>
            {
                ++sentPackages;
                _testOutputHelper.WriteLine($"Next package received: {sentPackages / (double)numberOfPackagesToSend * 100}%");
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

            response = await secondClient.httpClient.PostAsync("api/exchange/packageFile", formData);
            var packages = await response.Content.ReadFromJsonAsync<List<Package>>();
            numberOfPackagesToSend = packages.Count;
            await secondClient.clientConnection.InvokeAsync(nameof(MessageHub.StartSendingFile), packages.Select(p => p.Id).ToList(),
            packages.Count, cipherMode, 128);
            foreach (var package in packages)
            {
                await secondClient.clientConnection.InvokeAsync(nameof(MessageHub.SendPackage), package);
            }

            await secondClient.clientConnection.InvokeAsync(nameof(MessageHub.StopSendingFile), fileName);
            firstClientResetEvent.WaitOne();

            // Assert
            sentPackages.Should().Be(numberOfPackagesToSend); ;
        }

        [Theory]
        [InlineData(CipherMode.ECB)]
        [InlineData(CipherMode.CBC)]
        [InlineData(CipherMode.CFB)]
        [InlineData(CipherMode.OFB)]
        public async Task SendingFile_WhenClientsExchangedKeys_ReceivedFileShouldHaveSameExtension(CipherMode cipherMode)
        {
            // Arrange
            var originalBytes = new byte[1024 * 1024];
            new Random().NextBytes(originalBytes);
            var extension = "nonGenericExtension";
            var fileName = $"testFile.{extension}";
            var receivedFileName = string.Empty;
            var firstServer = BuildTestServer(clientName: "firstServer");
            _firstServer = firstServer;
            var secondServer = BuildTestServer(builder =>
            {
                builder.UseUrls("http://localhost:24555", "https://localhost:24556");
            }, "secondServer");
            _secondServer = secondServer;

            var fileManager = firstServer.Services.GetRequiredService<IFileManager>();
            var firstClient = await SetupConnectionsForBothServersAsync(firstServer, secondServer);
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

            var secondClient = await SetupConnectionsForBothServersAsync(secondServer, firstServer);
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

            response = await secondClient.httpClient.PostAsync("api/exchange/packageFile", formData);
            var packages = await response.Content.ReadFromJsonAsync<List<Package>>();
            await secondClient.clientConnection.InvokeAsync(nameof(MessageHub.StartSendingFile), packages.Select(p => p.Id).ToList(),
            packages.Count, cipherMode, 128);
            foreach (var package in packages)
            {
                await secondClient.clientConnection.InvokeAsync(nameof(MessageHub.SendPackage), package);
            }

            await secondClient.clientConnection.InvokeAsync(nameof(MessageHub.StopSendingFile), fileName);
            firstClientResetEvent.WaitOne();

            // Assert
            receivedFileName.Split(".")[^1].Should().Be(extension);
        }

        [Theory]
        [InlineData(CipherMode.ECB)]
        [InlineData(CipherMode.CBC)]
        [InlineData(CipherMode.CFB)]
        [InlineData(CipherMode.OFB)]
        public async Task SendingFile_WhenClientsExchangedKeys_ReceiverCurrentCipherModeShouldBeSelectedCipherMode(CipherMode cipherMode)
        {
            // Arrange
            var originalBytes = new byte[1024 * 1024];
            new Random().NextBytes(originalBytes);
            var extension = "nonGenericExtension";
            var fileName = $"testFile.{extension}";
            var receivedFileName = string.Empty;
            var firstServer = BuildTestServer(clientName: "firstServer");
            _firstServer = firstServer;
            var secondServer = BuildTestServer(builder =>
            {
                builder.UseUrls("http://localhost:24555", "https://localhost:24556");
            }, "secondServer");
            _secondServer = secondServer;

            var encryptor = firstServer.Services.GetRequiredService<IEncryptorService>();
            var firstClient = await SetupConnectionsForBothServersAsync(firstServer, secondServer);
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

            var secondClient = await SetupConnectionsForBothServersAsync(secondServer, firstServer);
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

            response = await secondClient.httpClient.PostAsync("api/exchange/packageFile", formData);
            var packages = await response.Content.ReadFromJsonAsync<List<Package>>();
            await secondClient.clientConnection.InvokeAsync(nameof(MessageHub.StartSendingFile), packages.Select(p => p.Id).ToList(),
            packages.Count, cipherMode, 128);
            foreach (var package in packages)
            {
                await secondClient.clientConnection.InvokeAsync(nameof(MessageHub.SendPackage), package);
            }

            await secondClient.clientConnection.InvokeAsync(nameof(MessageHub.StopSendingFile), fileName);
            firstClientResetEvent.WaitOne();

            // Assert
            encryptor.CurrentDataCipherMode.Should().Be(cipherMode);
        }

        public void Dispose()
        {
            if (_firstServer != null)
            {
                _firstServer.Services.GetService<IKeyStore>().DeleteKeys();
                _firstServer.Services.GetService<IFileManager>().DeleteAllFiles();
                _firstServer.Dispose();
            }
            if (_secondServer != null)
            {
                _secondServer.Services.GetService<IKeyStore>().DeleteKeys();
                _secondServer.Services.GetService<IFileManager>().DeleteAllFiles();
                _secondServer.Dispose();
            }
        }
    }
}
