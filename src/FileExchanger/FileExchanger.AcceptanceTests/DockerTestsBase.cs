using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ductus.FluentDocker.Builders;
using Ductus.FluentDocker.Services;
using Ductus.FluentDocker.Services.Extensions;
using FileExchanger.Hubs;
using FileExchanger.Requests;
using FileExchanger.Responses;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Xunit.Abstractions;

namespace FileExchanger.AcceptanceTests
{
    public class DockerTestsBase
    {
        protected ITestOutputHelper TestOutputHelper { get; }
        protected ICompositeService CompositeService { get; }
        private readonly string _dirSeparator;

        public DockerTestsBase(ITestOutputHelper testOutputHelper)
        {
            TestOutputHelper = testOutputHelper;
            _dirSeparator = string.Empty;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                _dirSeparator = "/";
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _dirSeparator = @"\";
            }
            var testAssemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var solutionPath = Directory.GetParent(testAssemblyPath.Substring(0, testAssemblyPath.LastIndexOf($@"{_dirSeparator}bin{_dirSeparator}", StringComparison.Ordinal))).FullName;
            var projectPath = $"{solutionPath}{_dirSeparator}FileExchanger";

            var composePath = $"{solutionPath}{_dirSeparator}acceptance-tests-docker-compose.yml";
            CompositeService = new Builder()
                .UseContainer()
                .UseCompose()
                .FromFile(composePath)
                .RemoveOrphans()
                .WaitForHttp("api1", "http://localhost:8006", continuation: (response, _) => response.Code != HttpStatusCode.OK ? 2000 : 0)
                .WaitForHttp("api2", "http://localhost:8008", continuation: (response, _) => response.Code != HttpStatusCode.OK ? 2000 : 0)
                .Build().Start();
        }

        protected async Task<byte[]> GetFileBytesFromContainer(IContainerService container, string fileName)
        {
            // Copy file form container to host
            Directory.CreateDirectory($@"{Environment.CurrentDirectory}{_dirSeparator}temp");
            container.CopyFrom($"/home/files/{fileName}",
                $@"{Environment.CurrentDirectory}\temp", true);

            // Assert received file content is equal to original content
            var fullFilePath = $@"{Environment.CurrentDirectory}{_dirSeparator}temp{_dirSeparator}{fileName}";
            var bytes = (await File.ReadAllBytesAsync(fullFilePath));
            File.Delete(fullFilePath);
            return bytes;
        }

        protected async Task<LoginResponse> LoginClientAsync(HttpClient client, string password)
        {
            var response = await client.PostAsJsonAsync("api/account", new LoginRequest
            {
                Password = password
            });
            return await response.Content.ReadFromJsonAsync<LoginResponse>();
        }

        protected async Task<(HttpClient httpClient, HubConnection hostConnection, HubConnection clientConnection)> SetupConnectionForBothServers(string hostUrl, string secondUrl)
        {
            (HttpClient httpClient, HubConnection hostConnection, HubConnection clientConnection) client =
            (
                new HttpClient
                {
                    BaseAddress = new Uri($"http://{hostUrl}"),
                    Timeout = TimeSpan.FromMinutes(10)
                },
                new HubConnectionBuilder()
                    .WithUrl($"ws://{hostUrl}/messages")
                    .Build(),
                new HubConnectionBuilder()
                    .WithUrl($"ws://{secondUrl}/messages")
                    .Build()
            );
            client.hostConnection.ServerTimeout = TimeSpan.FromMinutes(2);
            client.clientConnection.ServerTimeout = TimeSpan.FromMinutes(2);

            var hostResetEvent = new ManualResetEvent(false);

            client.hostConnection.On<bool>("ReceiveConnectAsHost", (b) =>
            {
                hostResetEvent.Set();
            });
            await client.hostConnection.StartAsync();
            await client.hostConnection.InvokeAsync(nameof(MessageHub.ConnectAsHost));
            await client.clientConnection.StartAsync();
            hostResetEvent.WaitOne();
            hostResetEvent.Reset();

            return client;
        }

        public void Dispose()
        {
            CompositeService.Stop();
            CompositeService.Dispose();
        }
    }
}
