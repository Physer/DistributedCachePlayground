using DCP.Logic;
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
            // Build dependencies
            using IHost host = CreateHostBuilder(args).Build();
            var cachedCommentService = host.Services.GetRequiredService<CachedCommentService>();
            var memoryCommentService = host.Services.GetRequiredService<MemoryCommentService>();
            var commentsRepository = host.Services.GetRequiredService<CommentsRepository>();

            // Parse input
            if (args.Length <= 0 && args.Length > 1)
            {
                Console.WriteLine("No valid options passed in... Exiting...");
                return;
            }

            if(!int.TryParse(args.First(), out var parsedOption))
            {
                Console.WriteLine("Invalid option selected, exiting...");
                return;
            }

            // Execute application flow
            ExecutionResult executionResult = null;
            switch(parsedOption)
            {
                // In-memory
                case 1:
                    executionResult = await ExecuteUsingMemory(memoryCommentService);
                    break;
                // Redis without locking
                case 2:
                    executionResult = await ExecuteUsingRedis(cachedCommentService, LockType.None);
                    break;
                // Redis with a Semaphore lock
                case 3:
                    executionResult = await ExecuteUsingRedis(cachedCommentService, LockType.Semaphore);
                    break;
                // Redis with Redlock.net
                case 4:
                    executionResult = await ExecuteUsingRedis(cachedCommentService, LockType.Redlock);
                    break;
                default:
                    Console.WriteLine("Unable to use the select option, exiting...");
                    return;
            }

            if(executionResult is null)
            {
                Console.WriteLine("Unable to retrieve the result, exiting...");
                return;
            }

            ResultsPrinter.PrintResults(executionResult);
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

        static async Task<ExecutionResult> ExecuteUsingMemory(MemoryCommentService commentService)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            var requests = new ConcurrentBag<Task<ThreadExecutionResult>>();
            Parallel.For(0, 200, _ => requests.Add(commentService.Execute()));
            var threadResults = await Task.WhenAll(requests);

            return new ExecutionResult
            {
                ThreadExecutionResults = threadResults,
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
                    services.AddSingleton<MemoryCommentService>();
                    services.AddTransient<CachedCommentService>();
                });
    }
}
