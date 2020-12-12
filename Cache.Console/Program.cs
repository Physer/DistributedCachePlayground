using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Cache.Console
{
    public class Program
    {
        static async Task Main(string[] args)
        {
            System.Console.WriteLine("Hello cache");
            // Build dependencies
            using IHost host = CreateHostBuilder(args).Build();
            var commentService = host.Services.GetRequiredService<CommentService>();
            var distributedCache = host.Services.GetRequiredService<IDistributedCache>();
            var cacheKey = "comments";
            
            // Remove any existing cache entries
            await distributedCache.RemoveAsync(cacheKey);

            // Execute a flow with only retrieving data from origin
            var originResult = await Execute(commentService, false, true);
            await distributedCache.RemoveAsync(cacheKey);

            // Execute the flow without any locks and clean up afterwards
            var noLockResult = await Execute(commentService, false);
            await distributedCache.RemoveAsync(cacheKey);

            // Execute the flow with a semaphore lock allowing for one thread's access and clean up afterwards
            var lockResult = await Execute(commentService, true);
            await distributedCache.RemoveAsync(cacheKey);

            // Results overview formatting
            System.Console.WriteLine($"------------------------------------------------------------------------------");
            System.Console.WriteLine($"| --                         Results overview                              -- |");
            System.Console.WriteLine($"------------------------------------------------------------------------------");
            System.Console.WriteLine($"|                                 -- --                                       |");
            System.Console.WriteLine($"|                           -- Introduction --                                |");
            System.Console.WriteLine($"| This overview presents data                                                 |");
            System.Console.WriteLine($"| about accessing an unlocked                                                 |");
            System.Console.WriteLine($"| resource in Redis with                                                      |");
            System.Console.WriteLine($"| multiple threads.                                                           |");
            System.Console.WriteLine($"|                                                                             |");
            System.Console.WriteLine($"| Every section providers the                                                 |");
            System.Console.WriteLine($"| amount of requests going to                                                 |");
            System.Console.WriteLine($"| the origin and to the cache.                                                |");
            System.Console.WriteLine($"|                                                                             |");
            System.Console.WriteLine($"| 200 threads are executed in                                                 |");
            System.Console.WriteLine($"| parallel, to simulate multiple                                              |");
            System.Console.WriteLine($"| threads accessing the same                                                  |");
            System.Console.WriteLine($"| resource at the same time.                                                  |");
            System.Console.WriteLine($"|                                                                             |");
            System.Console.WriteLine($"| The elapsed time is presented                                               |");
            System.Console.WriteLine($"| at the end of the section                                                   |");
            System.Console.WriteLine($"------------------------------------------------------------------------------");
            System.Console.WriteLine($"|                                 -- --                                       |");
            System.Console.WriteLine($"| From origin:                                                                |");
            System.Console.WriteLine($"| Total amount of requests: {originResult.ThreadExecutionResults.Count()}");
            System.Console.WriteLine($"| Requests to origin: {originResult.ThreadExecutionResults.Count(result => !result.GotResultFromCache)}");
            System.Console.WriteLine($"| Requests to Redis: {originResult.ThreadExecutionResults.Count(result => result.GotResultFromCache)}");
            System.Console.WriteLine($"| Elapsed miliseconds: {originResult.ElapsedMiliseconds} ms");
            System.Console.WriteLine($"|                                 -- --                                       |");
            System.Console.WriteLine($"------------------------------------------------------------------------------");
            System.Console.WriteLine($"|                                 -- --                                       |");
            System.Console.WriteLine($"| Without locking:                                                            |");
            System.Console.WriteLine($"| Total amount of requests: {noLockResult.ThreadExecutionResults.Count()}");
            System.Console.WriteLine($"| Requests to origin: {noLockResult.ThreadExecutionResults.Count(result => !result.GotResultFromCache)}");
            System.Console.WriteLine($"| Requests to Redis: {noLockResult.ThreadExecutionResults.Count(result => result.GotResultFromCache)}");
            System.Console.WriteLine($"| Elapsed miliseconds: {noLockResult.ElapsedMiliseconds} ms");
            System.Console.WriteLine($"|                                 -- --                                       |");
            System.Console.WriteLine($"------------------------------------------------------------------------------");
            System.Console.WriteLine($"|                                 -- --                                       |");
            System.Console.WriteLine($"| With a semaphore lock:                                                      |");
            System.Console.WriteLine($"| Total amount of requests: {lockResult.ThreadExecutionResults.Count()}");
            System.Console.WriteLine($"| Requests to origin: {lockResult.ThreadExecutionResults.Count(result => !result.GotResultFromCache)}");
            System.Console.WriteLine($"| Requests to Redis: {lockResult.ThreadExecutionResults.Count(result => result.GotResultFromCache)}");
            System.Console.WriteLine($"| Elapsed miliseconds: {lockResult.ElapsedMiliseconds} ms");
            System.Console.WriteLine($"|                                 -- --                                       |");
            System.Console.WriteLine($"------------------------------------------------------------------------------");

            System.Console.WriteLine("Goodbye cache");
            await host.RunAsync();
        }

        static async Task<ExecutionResult> Execute(CommentService commentService, bool withLock, bool alwaysUseOrigin = false)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            System.Console.WriteLine("Starting timer...");

            System.Console.WriteLine("Without locking...");
            var requests = new ConcurrentBag<Task<ThreadExecutionResult>>();
            Parallel.For(0, 200, _ => requests.Add(alwaysUseOrigin ? 
                                                    commentService.ExecuteFromOrigin() : 
                                                    (withLock ? commentService.ExecuteWithLock() : commentService.ExecuteWithoutLock())));
            var threadResults = await Task.WhenAll(requests);

            System.Console.WriteLine($"Time elapsed: {stopwatch.ElapsedMilliseconds} ms!");
            return new ExecutionResult
            {
                ThreadExecutionResults = threadResults,
                ElapsedMiliseconds = stopwatch.ElapsedMilliseconds
            };
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
