using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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

            //var firstClient = await SetupConnectionForBothServers("localhost:8006", "localhost:8008");
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

            //var secondClient = await SetupConnectionForBothServers("localhost:8008", "localhost:8006");
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
