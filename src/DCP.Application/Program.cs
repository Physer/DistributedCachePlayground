﻿using Microsoft.Extensions.Caching.Distributed;
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
            Console.WriteLine($"| Without locking:                                                            |");
            Console.WriteLine($"| Total amount of requests: {noLockResult.ThreadExecutionResults.Count()}");
            Console.WriteLine($"| Requests to origin: {noLockResult.ThreadExecutionResults.Count(result => !result.GotResultFromCache)}");
            Console.WriteLine($"| Requests to Redis: {noLockResult.ThreadExecutionResults.Count(result => result.GotResultFromCache)}");
            Console.WriteLine($"| Elapsed miliseconds: {noLockResult.ElapsedMiliseconds} ms");
            Console.WriteLine($"|                                 -- --                                       |");
            Console.WriteLine($"------------------------------------------------------------------------------");
            Console.WriteLine($"|                                 -- --                                       |");
            Console.WriteLine($"| With a semaphore lock:                                                      |");
            Console.WriteLine($"| Total amount of requests: {lockResult.ThreadExecutionResults.Count()}");
            Console.WriteLine($"| Requests to origin: {lockResult.ThreadExecutionResults.Count(result => !result.GotResultFromCache)}");
            Console.WriteLine($"| Requests to Redis: {lockResult.ThreadExecutionResults.Count(result => result.GotResultFromCache)}");
            Console.WriteLine($"| Elapsed miliseconds: {lockResult.ElapsedMiliseconds} ms");
            Console.WriteLine($"|                                 -- --                                       |");
            Console.WriteLine($"------------------------------------------------------------------------------");

            Console.WriteLine("Goodbye cache");
            await host.RunAsync();
        }

        static async Task<ExecutionResult> Execute(CommentService commentService, bool withLock, bool alwaysUseOrigin = false)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            Console.WriteLine("Starting timer...");

            Console.WriteLine("Without locking...");
            var requests = new ConcurrentBag<Task<ThreadExecutionResult>>();
            Parallel.For(0, 200, _ => requests.Add(alwaysUseOrigin ?
                                                    commentService.ExecuteFromOrigin() :
                                                    withLock ? commentService.ExecuteWithLock() : commentService.ExecuteWithoutLock()));
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

                    services.AddTransient<CommentService>();
                });
    }
}
