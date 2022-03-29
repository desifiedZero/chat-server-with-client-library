using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ServerApplication;
using System;
using System.Configuration;
using System.Net;

using IHost host = Host.CreateDefaultBuilder(args)
            .UseWindowsService(options =>
            {
                options.ServiceName = ".NET Chat Service";
            })
            .ConfigureServices(services =>
            {
                services.AddHostedService<WindowsBackgroundService>();
                services.AddSingleton<ChatService>();
                services.AddLogging();
            })
            .Build();

await host.RunAsync();
