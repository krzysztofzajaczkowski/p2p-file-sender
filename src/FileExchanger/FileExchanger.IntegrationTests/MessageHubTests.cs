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

    }
}
