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
            var commentService = host.Services.GetRequiredService<CommentService>();
            var noLockSw = await Execute(commentService, false);
            var lockSw = await Execute(commentService, true);

            System.Console.WriteLine($"Without any locking, time elapsed is: {noLockSw.ElapsedMilliseconds}");
            System.Console.WriteLine($"With semaphore locking, time elapsed is: {lockSw.ElapsedMilliseconds}");

            System.Console.WriteLine("Goodbye cache");
            await host.RunAsync();
        }

        static async Task<Stopwatch> Execute(CommentService commentService, bool withLock)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            System.Console.WriteLine("Starting timer...");

            System.Console.WriteLine("Without locking...");
            var requests = new ConcurrentBag<Task>();
            Parallel.For(0, 200, _ => requests.Add(withLock ? commentService.ExecuteWithLock() : commentService.ExecuteWithoutLock()));
            await Task.WhenAll(requests);

            System.Console.WriteLine($"Time elapsed: {stopwatch.ElapsedMilliseconds} ms!");
            return stopwatch;
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
