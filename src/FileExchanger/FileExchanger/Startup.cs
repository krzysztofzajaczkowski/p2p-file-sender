using System;
using System.Runtime.InteropServices;
using ElectronNET.API;
using ElectronNET.API.Entities;
using FileExchanger.Hubs;
using FileExchanger.Services.ConnectionStore;
using FileExchanger.Services.DummyData;
using FileExchanger.Services.Encryptor;
using FileExchanger.Services.FileManager;
using FileExchanger.Services.JavaScriptInterop;
using FileExchanger.Services.KeyStore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.SpaServices.AngularCli;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FileExchanger
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<IKeyStore, DirectoryKeyStore>();
            //services.AddSingleton<IKeyStore, InMemoryKeyStore>();
            services.AddSingleton<IConnectionStore, InMemoryConnectionStore>();
            services.AddSingleton<IDummyDataService, DummyDataService>();
            services.AddSingleton<IFileManager, DirectoryFileManager>();
            //services.AddSingleton<IFileManager, InMemoryFileManager>();
            services.AddSingleton<IEncryptorService, EncryptorService>();

            services.AddCors(o => o.AddPolicy("AllPolicy", b =>
            {
                b.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
            }));

            services.AddSignalR(o =>
            {
                o.EnableDetailedErrors = true;
                o.MaximumReceiveMessageSize = null;
                o.ClientTimeoutInterval = TimeSpan.FromMinutes(2);
            });

            services.Configure<FormOptions>(o =>
            {
                o.ValueLengthLimit = int.MaxValue;
                o.MultipartBodyLengthLimit = int.MaxValue;
                o.MemoryBufferThreshold = int.MaxValue;
            });

            services.Configure<KestrelServerOptions>(options =>
            {
                options.Limits.MaxRequestBodySize = int.MaxValue;
            });

            services.Configure<IISServerOptions>(options =>
            {
                options.MaxRequestBodySize = int.MaxValue;
            });

            services.AddNodeServices();

            services.AddSingleton<JavaScriptInteropService>();

            services.AddControllersWithViews();
            // In production, the Angular files will be served from this directory
            services.AddSpaStaticFiles(configuration =>
            {
                configuration.RootPath = "ClientApp/dist";

            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (Environment.GetEnvironmentVariable("RUN_ELECTRON") == "1" || Environment.GetEnvironmentVariable("RUN_ELECTRON") == null)
            {
                ElectronBootstrap();
            }
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
            }

            app.UseStaticFiles();
            if (!env.IsDevelopment())
            {
                app.UseSpaStaticFiles();
            }

            app.UseRouting();

            app.UseCors("AllPolicy");

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapHub<MessageHub>("messages");
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller}/{action=Index}/{id?}");
            });

            app.UseSpa(spa =>
            {
                // To learn more about options for serving an Angular SPA from ASP.NET Core,
                // see https://go.microsoft.com/fwlink/?linkid=864501

                spa.Options.SourcePath = env.ContentRootPath + "ClientApp";

                if (env.IsDevelopment())
                {
                    spa.UseAngularCliServer(npmScript: "start");
                }
            });
        }

        public async void ElectronBootstrap()
        {
            BrowserWindowOptions options = new BrowserWindowOptions
            {
                Show = false
            };
            BrowserWindow mainWindow = await Electron.WindowManager.CreateWindowAsync();
            mainWindow.OnReadyToShow += () =>
            {
                mainWindow.Show();
            };
            mainWindow.SetTitle("App Name here");
            MenuItem[] menu = new MenuItem[]
            {
                new MenuItem
                {
                    Label = "File",
                    Submenu=new MenuItem[]
                    {
                        new MenuItem
                        {
                            Label ="Exit",
                            Click =()=>{Electron.App.Exit();}
                        }
                    }
                },
                new MenuItem
                {
                    Label = "Info",
                    Click = async ()=>
                    {
                        await Electron.Dialog.ShowMessageBoxAsync("Welcome to App");
                    }
                }
            };

            Electron.Menu.SetApplicationMenu(menu);
        }
    }
}
