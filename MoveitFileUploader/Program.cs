using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MoveitFileUploaderLib;
using System;
using System.Threading.Tasks;

namespace MoveitFileUploader
{
    class Program
    {
        static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((_, services) =>
                {
                    services.AddHttpClient();
                    services.AddTransient<MoveitFileUploaderApp>();
                    services.AddLogging(configure => configure.AddConsole());
                })
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddConsole();
                })
                .ConfigureServices(async (context, services) =>
                {
                    var app = services.BuildServiceProvider().GetRequiredService<MoveitFileUploaderApp>();
                    await app.RunAsync();
                });
    }
}
