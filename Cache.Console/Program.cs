using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Cache.Console
{
    public class Program
    {
        static async Task Main(string[] args)
        {
            System.Console.WriteLine("Hello cache");
            using IHost host = CreateHostBuilder(args).Build();
            var cacheService = host.Services.GetRequiredService<CommentService>();

            var stopwatch = new Stopwatch();
            stopwatch.Start();
            System.Console.WriteLine("Starting timer...");

            var requests = new ConcurrentBag<Task>();
            Parallel.For(0, 200, _ => requests.Add(cacheService.Execute()));
            await Task.WhenAll(requests);

            System.Console.WriteLine($"Time elapsed: {stopwatch.ElapsedMilliseconds} ms!");
            await host.RunAsync();
            System.Console.WriteLine("Goodbye cache");
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
