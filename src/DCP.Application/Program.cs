using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
            var results = new List<ExecutionResult>();
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
            results.Add(memoryResult);

            // Remove any existing cache entries
            await distributedCache.RemoveAsync(cacheKey);

            // Execute the flow without any locks and clean up afterwards
            var withoutLockResult = await ExecuteUsingRedis(cachedCommentService, LockType.None);
            await distributedCache.RemoveAsync(cacheKey);
            results.Add(withoutLockResult);

            // Execute the flow with a semaphore lock allowing for one thread's access and clean up afterwards
            var withSemaphoreLockResult = await ExecuteUsingRedis(cachedCommentService, LockType.Semaphore);
            await distributedCache.RemoveAsync(cacheKey);
            results.Add(withSemaphoreLockResult);

            // Execute the flow with Redlock.net allowing for one thread's access and clean up afterwards
            var withRedlockLockResult = await ExecuteUsingRedis(cachedCommentService, LockType.Redlock);
            await distributedCache.RemoveAsync(cacheKey);
            results.Add(withRedlockLockResult);

            ResultsPrinter.PrintResults(results);
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
                ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
                ResultTitle = $"Results using Redis cache with lock: {lockType}"
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
                ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
                ResultTitle = $"Results using in-memory"
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
