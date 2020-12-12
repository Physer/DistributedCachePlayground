using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Threading.Tasks;

namespace Cache.Console
{
    public class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Hello cache");
            using IHost host = CreateHostBuilder(args).Build();
            var cacheService = host.Services.GetRequiredService<CommentService>();

            await cacheService.Execute();
            await host.RunAsync();
            Console.WriteLine("Goodbye cache");
        }

        static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((_, services) =>
                {
                    services.AddHttpClient();
                    services.AddStackExchangeRedisCache(options => options.Configuration = "localhost");

                    services.AddTransient<CommentService>();
                });
    }
}
