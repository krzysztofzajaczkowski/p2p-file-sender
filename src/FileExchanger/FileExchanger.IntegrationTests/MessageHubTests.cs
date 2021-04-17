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

    }
}
