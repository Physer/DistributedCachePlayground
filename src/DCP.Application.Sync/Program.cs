using DCP.Logic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace DCP.Application.Sync
{
    public class Program
    {
        static void Main(string[] args)
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

            if (!int.TryParse(args.FirstOrDefault(), out var parsedOption))
            {
                Console.WriteLine("Invalid option selected, exiting...");
                return;
            }

            // Execute application flow
            ExecutionResult executionResult = null;
            switch (parsedOption)
            {
                // In-memory without locking
                case 1:
                    executionResult = ExecuteUsingMemory(memoryCommentService);
                    break;
                default:
                    Console.WriteLine("Unable to use the select option, exiting...");
                    return;
            }

            if (executionResult is null)
            {
                Console.WriteLine("Unable to retrieve the result, exiting...");
                return;
            }
            
            ResultsPrinter.PrintResults(executionResult);

            host.Run();
        }

        static ExecutionResult ExecuteUsingMemory(MemoryCommentService commentService)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            var requests = new ConcurrentBag<ThreadExecutionResult>();
            Parallel.For(0, 200, _ => requests.Add(commentService.Execute()));

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
                    services.AddSingleton<MemoryCommentService>();
                    services.AddTransient<CachedCommentService>();
                });
    }
}
