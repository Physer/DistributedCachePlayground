using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace DCP.Application
{
    public class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Hello cache");
            // Build dependencies
            using IHost host = CreateHostBuilder(args).Build();
            var cachedCommentService = host.Services.GetRequiredService<CachedCommentService>();
            var distributedCache = host.Services.GetRequiredService<IDistributedCache>();
            var cacheKey = "comments";

            // Load data in memory

            // Remove any existing cache entries
            await distributedCache.RemoveAsync(cacheKey);

            // Execute a flow with only retrieving data from origin
            var originResult = await ExecuteUsingRedis(cachedCommentService, LockType.Unknown, true);
            await distributedCache.RemoveAsync(cacheKey);

            // Execute the flow without any locks and clean up afterwards
            var withoutLockResult = await ExecuteUsingRedis(cachedCommentService, LockType.None);
            await distributedCache.RemoveAsync(cacheKey);

            // Execute the flow with a semaphore lock allowing for one thread's access and clean up afterwards
            var withSemaphoreLockResult = await ExecuteUsingRedis(cachedCommentService, LockType.Semaphore);
            await distributedCache.RemoveAsync(cacheKey);

            // Execute the flow with Redlock.net allowing for one thread's access and clean up afterwards
            var withRedlockLockResult = await ExecuteUsingRedis(cachedCommentService, LockType.Redlock);
            await distributedCache.RemoveAsync(cacheKey);

            // Results overview formatting
            Console.WriteLine($"------------------------------------------------------------------------------");
            Console.WriteLine($"| --                         Results overview                              -- |");
            Console.WriteLine($"------------------------------------------------------------------------------");
            Console.WriteLine($"|                                 -- --                                       |");
            Console.WriteLine($"|                           -- Introduction --                                |");
            Console.WriteLine($"| This overview presents data                                                 |");
            Console.WriteLine($"| about accessing an unlocked                                                 |");
            Console.WriteLine($"| resource in Redis with                                                      |");
            Console.WriteLine($"| multiple threads.                                                           |");
            Console.WriteLine($"|                                                                             |");
            Console.WriteLine($"| Every section providers the                                                 |");
            Console.WriteLine($"| amount of requests going to                                                 |");
            Console.WriteLine($"| the origin and to the cache.                                                |");
            Console.WriteLine($"|                                                                             |");
            Console.WriteLine($"| 200 threads are executed in                                                 |");
            Console.WriteLine($"| parallel, to simulate multiple                                              |");
            Console.WriteLine($"| threads accessing the same                                                  |");
            Console.WriteLine($"| resource at the same time.                                                  |");
            Console.WriteLine($"|                                                                             |");
            Console.WriteLine($"| The elapsed time is presented                                               |");
            Console.WriteLine($"| at the end of the section.                                                  |");
            Console.WriteLine($"------------------------------------------------------------------------------");
            Console.WriteLine($"|                                 -- --                                       |");
            Console.WriteLine($"| From origin:                                                                |");
            Console.WriteLine($"| Total amount of requests: {originResult.ThreadExecutionResults.Count()}");
            Console.WriteLine($"| Requests to origin: {originResult.ThreadExecutionResults.Count(result => !result.GotResultFromCache)}");
            Console.WriteLine($"| Requests to Redis: {originResult.ThreadExecutionResults.Count(result => result.GotResultFromCache)}");
            Console.WriteLine($"| Elapsed miliseconds: {originResult.ElapsedMiliseconds} ms");
            Console.WriteLine($"|                                 -- --                                       |");
            Console.WriteLine($"------------------------------------------------------------------------------");
            Console.WriteLine($"|                                 -- --                                       |");
            Console.WriteLine($"|                           -- Using memory --                                |");
            Console.WriteLine($"|                                 -- --                                       |");
            Console.WriteLine($"| Note that amount of requests are not shown here as this is already loaded   |");
            Console.WriteLine($"| into memory at the start of the application.                                |");
            Console.WriteLine($"|                                                                             |");
            Console.WriteLine($"| Only the elapsed miliseconds to retrieve the data are relevant.             |");
            Console.WriteLine($"| Elapsed miliseconds: ms");
            Console.WriteLine($"|                                 -- --                                       |");
            Console.WriteLine($"------------------------------------------------------------------------------");
            Console.WriteLine($"|                                 -- --                                       |");
            Console.WriteLine($"|                           -- Using Redis --                                 |");
            Console.WriteLine($"|                                 -- --                                       |");
            Console.WriteLine($"------------------------------------------------------------------------------");
            Console.WriteLine($"|                                 -- --                                       |");
            Console.WriteLine($"| Without locking:                                                            |");
            Console.WriteLine($"| Total amount of requests: {withoutLockResult.ThreadExecutionResults.Count()}");
            Console.WriteLine($"| Requests to origin: {withoutLockResult.ThreadExecutionResults.Count(result => !result.GotResultFromCache)}");
            Console.WriteLine($"| Requests to Redis: {withoutLockResult.ThreadExecutionResults.Count(result => result.GotResultFromCache)}");
            Console.WriteLine($"| Elapsed miliseconds: {withoutLockResult.ElapsedMiliseconds} ms");
            Console.WriteLine($"|                                 -- --                                       |");
            Console.WriteLine($"------------------------------------------------------------------------------");
            Console.WriteLine($"|                                 -- --                                       |");
            Console.WriteLine($"| With a semaphore lock:                                                      |");
            Console.WriteLine($"| Total amount of requests: {withSemaphoreLockResult.ThreadExecutionResults.Count()}");
            Console.WriteLine($"| Requests to origin: {withSemaphoreLockResult.ThreadExecutionResults.Count(result => !result.GotResultFromCache)}");
            Console.WriteLine($"| Requests to Redis: {withSemaphoreLockResult.ThreadExecutionResults.Count(result => result.GotResultFromCache)}");
            Console.WriteLine($"| Elapsed miliseconds: {withSemaphoreLockResult.ElapsedMiliseconds} ms");
            Console.WriteLine($"|                                 -- --                                       |");
            Console.WriteLine($"------------------------------------------------------------------------------");
            Console.WriteLine($"|                                 -- --                                       |");
            Console.WriteLine($"| With Redlock.Net:                                                      |");
            Console.WriteLine($"| Total amount of requests: {withRedlockLockResult.ThreadExecutionResults.Count()}");
            Console.WriteLine($"| Requests to origin: {withRedlockLockResult.ThreadExecutionResults.Count(result => !result.GotResultFromCache)}");
            Console.WriteLine($"| Requests to Redis: {withRedlockLockResult.ThreadExecutionResults.Count(result => result.GotResultFromCache)}");
            Console.WriteLine($"| Elapsed miliseconds: {withRedlockLockResult.ElapsedMiliseconds} ms");
            Console.WriteLine($"|                                 -- --                                       |");
            Console.WriteLine($"------------------------------------------------------------------------------");

            Console.WriteLine("Goodbye cache");
            await host.RunAsync();
        }

        static async Task<ExecutionResult> ExecuteUsingRedis(CachedCommentService commentService, LockType lockType, bool alwaysUseOrigin = false)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            Console.WriteLine("Starting timer...");

            var requests = new ConcurrentBag<Task<ThreadExecutionResult>>();
            Parallel.For(0, 200, _ => requests.Add(commentService.Execute(lockType, alwaysUseOrigin)));
            var threadResults = await Task.WhenAll(requests);

            Console.WriteLine($"Time elapsed: {stopwatch.ElapsedMilliseconds} ms!");
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

                    services.AddTransient<CachedCommentService>();
                });
    }
}
