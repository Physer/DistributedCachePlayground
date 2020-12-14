using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
            var memoryCommentService = host.Services.GetRequiredService<MemoryCommentService>();
            var distributedCache = host.Services.GetRequiredService<IDistributedCache>();
            var commentsRepository = host.Services.GetRequiredService<CommentsRepository>();
            var cacheKey = "comments";

            // Load data in memory
            var originStopwatch = new Stopwatch();
            originStopwatch.Start();
            var comments = (await commentsRepository.GetComments()).ToList();
            originStopwatch.Stop();

            // Retrieve the data from memory
            var memoryResult = ExecuteUsingMemory(memoryCommentService, comments);

            // Remove any existing cache entries
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
            Console.WriteLine($"|                                 -- --                                       |");
            Console.WriteLine($"| This overview presents data about accessing an unlocked resource in Redis   |");
            Console.WriteLine($"| with multiple threads.                                                      |");
            Console.WriteLine($"|                                                                             |");
            Console.WriteLine($"| Every section providers the amount of requests going to the origin          |");
            Console.WriteLine($"| and to the cache.                                                           |");
            Console.WriteLine($"|                                                                             |");
            Console.WriteLine($"| 200 threads are executed in parallel, to simulate multiple threads          |");
            Console.WriteLine($"| accessing the same resource at the same time.                               |");
            Console.WriteLine($"|                                                                             |");
            Console.WriteLine($"| The elapsed time is presented at the end of the section.                    |");
            Console.WriteLine($"------------------------------------------------------------------------------");
            Console.WriteLine($"|                                 -- --                                       |");
            Console.WriteLine($"| Retrieving comments from origin and storing in application memory.          |");
            Console.WriteLine($"| Elapsed miliseconds: {originStopwatch.ElapsedMilliseconds} ms");
            Console.WriteLine($"|                                 -- --                                       |");
            Console.WriteLine($"------------------------------------------------------------------------------");
            Console.WriteLine($"|                                 -- --                                       |");
            Console.WriteLine($"|                           -- Using memory --                                |");
            Console.WriteLine($"|                                 -- --                                       |");
            Console.WriteLine($"| Only the elapsed miliseconds to retrieve the data are relevant.             |");
            Console.WriteLine($"| Total amount of requests: {memoryResult.ThreadExecutionResults.Count()}");
            Console.WriteLine($"| Requests to origin: {memoryResult.ThreadExecutionResults.Count(result => !result.GotResultFromCache)}");
            Console.WriteLine($"| Requests to memory references: {memoryResult.ThreadExecutionResults.Count(result => result.GotResultFromCache)}");
            Console.WriteLine($"| Elapsed miliseconds: {memoryResult.ElapsedMilliseconds} ms");
            Console.WriteLine($"|                                 -- --                                       |");
            Console.WriteLine($"------------------------------------------------------------------------------");
            Console.WriteLine($"|                                 -- --                                       |");
            Console.WriteLine($"|                      -- Using Origin & Redis --                             |");
            Console.WriteLine($"|                                 -- --                                       |");
            Console.WriteLine($"------------------------------------------------------------------------------");
            Console.WriteLine($"|                                 -- --                                       |");
            Console.WriteLine($"| Without locking:                                                            |");
            Console.WriteLine($"| Total amount of requests: {withoutLockResult.ThreadExecutionResults.Count()}");
            Console.WriteLine($"| Requests to origin: {withoutLockResult.ThreadExecutionResults.Count(result => !result.GotResultFromCache)}");
            Console.WriteLine($"| Requests to Redis: {withoutLockResult.ThreadExecutionResults.Count(result => result.GotResultFromCache)}");
            Console.WriteLine($"| Elapsed miliseconds: {withoutLockResult.ElapsedMilliseconds} ms");
            Console.WriteLine($"|                                 -- --                                       |");
            Console.WriteLine($"------------------------------------------------------------------------------");
            Console.WriteLine($"|                                 -- --                                       |");
            Console.WriteLine($"| With a semaphore lock:                                                      |");
            Console.WriteLine($"| Total amount of requests: {withSemaphoreLockResult.ThreadExecutionResults.Count()}");
            Console.WriteLine($"| Requests to origin: {withSemaphoreLockResult.ThreadExecutionResults.Count(result => !result.GotResultFromCache)}");
            Console.WriteLine($"| Requests to Redis: {withSemaphoreLockResult.ThreadExecutionResults.Count(result => result.GotResultFromCache)}");
            Console.WriteLine($"| Elapsed miliseconds: {withSemaphoreLockResult.ElapsedMilliseconds} ms");
            Console.WriteLine($"|                                 -- --                                       |");
            Console.WriteLine($"------------------------------------------------------------------------------");
            Console.WriteLine($"|                                 -- --                                       |");
            Console.WriteLine($"| With Redlock.Net:                                                      |");
            Console.WriteLine($"| Total amount of requests: {withRedlockLockResult.ThreadExecutionResults.Count()}");
            Console.WriteLine($"| Requests to origin: {withRedlockLockResult.ThreadExecutionResults.Count(result => !result.GotResultFromCache)}");
            Console.WriteLine($"| Requests to Redis: {withRedlockLockResult.ThreadExecutionResults.Count(result => result.GotResultFromCache)}");
            Console.WriteLine($"| Elapsed miliseconds: {withRedlockLockResult.ElapsedMilliseconds} ms");
            Console.WriteLine($"|                                 -- --                                       |");
            Console.WriteLine($"------------------------------------------------------------------------------");

            Console.WriteLine("Goodbye cache");
            await host.RunAsync();
        }

        static async Task<ExecutionResult> ExecuteUsingRedis(CachedCommentService commentService, LockType lockType, bool alwaysUseOrigin = false)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            var requests = new ConcurrentBag<Task<ThreadExecutionResult>>();
            Parallel.For(0, 200, _ => requests.Add(commentService.Execute(lockType, alwaysUseOrigin)));
            var threadResults = await Task.WhenAll(requests);

            return new ExecutionResult
            {
                ThreadExecutionResults = threadResults,
                ElapsedMilliseconds = stopwatch.ElapsedMilliseconds
            };
        }

        static ExecutionResult ExecuteUsingMemory(MemoryCommentService commentService, IEnumerable<Comment> comments)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            var requests = new ConcurrentBag<ThreadExecutionResult>();
            Parallel.For(0, 200, _ => requests.Add(commentService.Execute(comments)));

            return new ExecutionResult
            {
                ThreadExecutionResults = requests,
                ElapsedMilliseconds = stopwatch.ElapsedMilliseconds
            };
        }

        static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((_, services) =>
                {
                    services.AddHttpClient();
                    services.AddStackExchangeRedisCache(options => options.Configuration = "localhost");

                    services.AddSingleton<CommentsRepository>();
                    services.AddTransient<MemoryCommentService>();
                    services.AddTransient<CachedCommentService>();
                });
    }
}
