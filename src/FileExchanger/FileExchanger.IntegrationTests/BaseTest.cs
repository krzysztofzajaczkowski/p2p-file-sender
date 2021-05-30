using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FileExchanger.Hubs;
using FileExchanger.Requests;
using FileExchanger.Responses;
using FileExchanger.Services.FileManager;
using FileExchanger.Services.KeyStore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FileExchanger.IntegrationTests
{
    public class BaseTest
    {
        private static string GetProjectPath(string projectRelativePath, Assembly startupAssembly)
        {
            // Get name of the target project which we want to test
            var projectName = startupAssembly.GetName().Name;

            // Get currently executing test project path
            var applicationBasePath = AppContext.BaseDirectory;

            // Find the path to the target project
            var directoryInfo = new DirectoryInfo(applicationBasePath);
            do
            {
                directoryInfo = directoryInfo.Parent;

                var projectDirectoryInfo = new DirectoryInfo(Path.Combine(directoryInfo.FullName, projectRelativePath));
                if (projectDirectoryInfo.Exists)
                {
                    var projectFileInfo = new FileInfo(Path.Combine(projectDirectoryInfo.FullName, projectName, $"{projectName}.csproj"));
                    if (projectFileInfo.Exists)
                    {
                        return Path.Combine(projectDirectoryInfo.FullName, projectName);
                    }
                }
            }
            while (directoryInfo.Parent != null);

            throw new Exception($"Project root could not be located using the application root {applicationBasePath}.");
        }
        protected TestServer BuildTestServer(Action<IWebHostBuilder> builderAppendix = null, string clientName = "client")
        {
            var dirSeparator = string.Empty; 
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                dirSeparator = "/";
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                dirSeparator = @"\";
            }
            var testAssemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var solutionPath = Directory.GetParent(testAssemblyPath.Substring(0, testAssemblyPath.LastIndexOf($@"{dirSeparator}bin{dirSeparator}", StringComparison.Ordinal))).FullName;
            var clientAngularPath = Path.Join(solutionPath, "FileExchanger/ClientApp/dist");
            Directory.CreateDirectory(clientAngularPath);
            var appsettingsPath = Path.Join(solutionPath, "FileExchanger");

            // Important to ensure that npm loads and is pointing to correct directory
            Directory.SetCurrentDirectory(clientAngularPath);

            var webHostBuilder = new WebHostBuilder()
                .UseEnvironment("Development")
                .UseWebRoot(clientAngularPath)
                .ConfigureServices(services =>
                {
                    services.AddNodeServices(o =>
                    {
                        var location = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                        o.ProjectPath = location;
                    });
                })
                .ConfigureTestServices(services =>
                {
                    var keyStoreProviderDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IKeyStore));
                    if (keyStoreProviderDescriptor != null)
                    {
                        services.Remove(keyStoreProviderDescriptor);
                    }
                    var dirKeyStore = new DirectoryKeyStore(clientName);
                    services.AddSingleton<IKeyStore>(dirKeyStore);

                    var fileManagerProviderDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IFileManager));
                    if (fileManagerProviderDescriptor != null)
                    {
                        services.Remove(fileManagerProviderDescriptor);
                    }
                    var dirFileManager = new DirectoryFileManager(clientName);
                    services.AddSingleton<IFileManager>(dirFileManager);
                })
                .UseConfiguration(new ConfigurationBuilder()
                    .SetBasePath(appsettingsPath)
                    .AddJsonFile("appsettings.json")
                    .Build())
                .UseStartup<Startup>();
            builderAppendix?.Invoke(webHostBuilder);
            return new TestServer(webHostBuilder);
        }

        protected HubConnection BuildHubConnection(TestServer server)
        {
            return new HubConnectionBuilder()
                .WithUrl(
                    "ws://localhost/messages",
                    c => c.HttpMessageHandlerFactory = _ => server.CreateHandler())
                .Build();
        }

        protected (HttpClient httpClient, HubConnection hostConnection) CreateClientConnection(TestServer hostServer)
        {
            return (httpClient: hostServer.CreateClient(),
                hostConnection: BuildHubConnection(hostServer));
        }

        protected (HttpClient httpClient, HubConnection hostConnection, HubConnection clientConnection)
            CreateConnectionsForBothServers(TestServer hostServer, TestServer clientServer)
        {
            return (httpClient: hostServer.CreateClient(),
                hostConnection: BuildHubConnection(hostServer),
                clientConnection: BuildHubConnection(clientServer));
        }

        protected async Task<(HttpClient httpClient, HubConnection hostConnection, HubConnection clientConnection)>
            SetupConnectionsForBothServersAsync(TestServer hostServer, TestServer clientServer)
        {
            var hostResetEvent = new ManualResetEvent(false);
            var client = (httpClient: hostServer.CreateClient(),
                hostConnection: BuildHubConnection(hostServer),
                clientConnection: BuildHubConnection(clientServer));

            client.hostConnection.On<bool>("ReceiveConnectAsHost", (b) =>
            {
                hostResetEvent.Set();
            });

            await client.hostConnection.StartAsync();
            await client.clientConnection.StartAsync();

            await client.hostConnection.InvokeAsync(nameof(MessageHub.ConnectAsHost));
            hostResetEvent.WaitOne();

            return client;
        }

        protected async Task<LoginResponse> LoginClientAsync(HttpClient client, string password)
        {
            var response = await client.PostAsJsonAsync("api/account", new LoginRequest
            {
                Password = password
            });
            return await response.Content.ReadFromJsonAsync<LoginResponse>();
        }
    }
}
