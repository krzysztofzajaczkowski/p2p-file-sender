using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Xunit;

namespace FileExchanger.IntegrationTests
{
    public class MessageHubTests
    {
        private TestServer BuildTestServer()
        {
            var webHostBuilder = new WebHostBuilder()
                .UseStartup<Startup>();
            return new TestServer(webHostBuilder);
        }

        private HubConnection BuildHubConnection(TestServer server)
        {
            return new HubConnectionBuilder()
                .WithUrl(
                    "http://localhost/messages",
                    c => c.HttpMessageHandlerFactory = _ => server.CreateHandler())
                .Build();
        }

        [Fact]
        public async Task Message_hub_should_reply_with_the_same_message_when_client_invoke_send_message_to_all_method()
        {
            // Arrange
            var messageReceivedEvent = new ManualResetEvent(false);
            const string message = "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Curabitur interdum quis enim a maximus.";
            var receivedMessage = string.Empty;

            var server = BuildTestServer();
            var client = BuildHubConnection(server);

            client.On<string>("ReceiveSendMessageToAll", msg =>
            {
                receivedMessage = msg;
                messageReceivedEvent.Set();
            });

            // Act
            await client.StartAsync();
            await client.InvokeAsync("SendMessageToAll", message);

            messageReceivedEvent.WaitOne();

            // Assert
            receivedMessage.Should().Be(message);
        }

        [Fact]
        public async Task Message_hub_should_reply_false_when_client_invoke_check_if_im_host_method()
        {
            // Arrange
            var messageReceivedEvent = new ManualResetEvent(false);
            bool? isHost = null;
            var server = BuildTestServer();
            var client = BuildHubConnection(server);

            client.On<bool>("ReceiveCheckIfImHost", msg =>
            {
                isHost = msg;
                messageReceivedEvent.Set();
            });

            // Act
            await client.StartAsync();
            await client.InvokeAsync("CheckIfImHost");

            messageReceivedEvent.WaitOne();

            // Assert
            isHost.Should().BeFalse();
        }

        [Fact]
        public async Task Message_hub_should_reply_true_when_client_invoke_connect_as_host_and_check_if_im_host_method()
        {
            // Arrange
            var messageReceivedEvent = new ManualResetEvent(false);
            bool? isHost = null;

            var server = BuildTestServer();
            var client = BuildHubConnection(server);

            client.On<bool>("ReceiveCheckIfImHost", msg =>
            {
                isHost = msg;
                messageReceivedEvent.Set();
            });

            // Act
            await client.StartAsync();
            await client.InvokeAsync("ConnectAsHost");
            await client.InvokeAsync("CheckIfImHost");

            messageReceivedEvent.WaitOne();

            // Assert
            isHost.Should().BeTrue();
        }

        [Fact]
        public async Task Message_hub_should_reply_true_to_first_client_and_false_to_second_client_when_invoke_check_if_im_host_method()
        {
            // Arrange
            var messageReceivedInFirstClientEvent = new ManualResetEvent(false);
            var messageReceivedInSecondClientEvent = new ManualResetEvent(false);

            bool? isFirstClientHost = null;
            bool? isSecondClientHost = null;

            var server = BuildTestServer();
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
            await firstClient.InvokeAsync("ConnectAsHost");
            await firstClient.InvokeAsync("CheckIfImHost");

            await secondClient.StartAsync();
            await secondClient.InvokeAsync("CheckIfImHost");

            messageReceivedInFirstClientEvent.WaitOne();
            messageReceivedInSecondClientEvent.WaitOne();

            // Assert
            isFirstClientHost.Should().BeTrue();
            isSecondClientHost.Should().BeFalse();
        }

        [Fact]
        public async Task First_client_should_receive_message_from_second_client_when_invoke_send_message_method()
        {
            // Arrange
            var message = "Test message from 2nd client";
            var receivedMessage = string.Empty;
            bool? firstClientIsHost = null;
            var firstClientConnectedAsHostEvent = new ManualResetEvent(false);
            var firstClientReceivedMessageEvent = new ManualResetEvent(false);

            var server = BuildTestServer();
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

            await firstConnection.SendAsync("ConnectAsHost");
            firstClientConnectedAsHostEvent.WaitOne();

            await secondConnection.SendAsync("SendMessage", message);
            firstClientReceivedMessageEvent.WaitOne();

            // Assert
            firstClientIsHost.Should().BeTrue();
            receivedMessage.Should().Be(message);
        }

        [Fact]
        public async Task First_client_should_receive_all_data_successfully()
        {
            // Arrange
            var fileName = "testFileName.txt";
            var message =
                "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Curabitur interdum quis enim a maximus. " +
                "Nulla vel leo at sapien vehicula commodo. Sed quis ipsum metus. Vivamus feugiat nisl vulputate suscipit volutpat. " +
                "Pellentesque habitant morbi tristique senectus et netus et malesuada fames ac turpis egestas. Duis nec ornare sapien. " +
                "Pellentesque et pretium erat. Suspendisse a commodo est. Nunc ut dolor.";
            var receivedFileName = string.Empty;
            var receivedNumberOfPackagesToSend = 0;
            var receivedPackages = 0;
            var receivedData = new List<byte>();
            int numberOfPackagesToSend;
            var buffer = new byte[2048];
            var readOffset = 0;
            var packageNumber = 0;
            var str = new MemoryStream(Encoding.UTF8.GetBytes(message));
            long remainingBytesToRead;

            var hostSetEvent = new ManualResetEvent(false);
            var startSendEvent = new ManualResetEvent(false);
            var stopSendEvent = new ManualResetEvent(false);

            var server = BuildTestServer();
            var firstClient = BuildHubConnection(server);
            var secondClient = BuildHubConnection(server);

            firstClient.On<bool>("ReceiveConnectAsHost", msg =>
            {
                hostSetEvent.Set();
            });

            firstClient.On<int, string>("ReceiveStartSending", (i, s) =>
            {
                receivedNumberOfPackagesToSend = i;
                receivedFileName = s;
                startSendEvent.Set();
            });

            firstClient.On<int, byte[]>("ReceiveSendPackage", (i, bytes) =>
            {
                ++receivedPackages;
                receivedData.AddRange(bytes);
            });

            firstClient.On("ReceiveStopSending", () =>
            {
                stopSendEvent.Set();
            });

            // Act
            await firstClient.StartAsync();
            await secondClient.StartAsync();
            await firstClient.SendAsync("ConnectAsHost");
            hostSetEvent.WaitOne();

            numberOfPackagesToSend = (int)Math.Ceiling(str.Length / (decimal)buffer.Length);
            await secondClient.SendAsync("StartSending", numberOfPackagesToSend, fileName);
            startSendEvent.WaitOne();

            remainingBytesToRead = str.Length;
            while (remainingBytesToRead > 0)
            {
                var read = str.Read(buffer, readOffset, buffer.Length);
                remainingBytesToRead -= read;
                await secondClient.SendAsync("SendPackage", packageNumber, buffer.Take(read).ToArray());
            }
            await secondClient.SendAsync("StopSending");
            stopSendEvent.WaitOne();

            // Assert
            Encoding.UTF8.GetString(receivedData.ToArray()).Should().Be(message);
            receivedData.ToArray().Should().Equal(Encoding.UTF8.GetBytes(message));
            receivedNumberOfPackagesToSend.Should().Be(numberOfPackagesToSend);
            receivedPackages.Should().Be(numberOfPackagesToSend);
            receivedFileName.Should().Be(fileName);
        }
    }
}
