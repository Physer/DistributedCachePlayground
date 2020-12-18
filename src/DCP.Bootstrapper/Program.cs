using DCP.Logic;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using static System.Console;

namespace DCP.Bootstrapper
{
    class Program
    {
        static async Task Main(string[] args)
        {
            WriteLine("Distributed Cache Playground | Bootstrapper");
            WriteLine($"------------------------------------------------------------------------------");

            using IHost host = CreateHostBuilder(args).Build();
            var distributedCache = host.Services.GetRequiredService<IDistributedCache>();
            var cacheKey = "comments";

            WriteLine("Cleaning cache from potential previous runs...");
            await distributedCache.RemoveAsync(cacheKey);
            WriteLine($"------------------------------------------------------------------------------");

            WriteLine("Please select how to run the benchmark:");
            WriteLine("1. Asynchronously");
            WriteLine("2. Synchronously");
            if (!int.TryParse(ReadLine(), out var parsedSynchronizationOption) || (parsedSynchronizationOption < 1 && parsedSynchronizationOption > 2))
            {
                WriteLine("You have selected an invalid number, please restart the program");
                return;
            }
            WriteLine($"You have selected option {parsedSynchronizationOption}");
            WriteLine($"------------------------------------------------------------------------------");

            switch (parsedSynchronizationOption)
            {
                case 1:
                    RunAsync();
                    break;
                case 2:
                    RunSync();
                    break;
                default:
                    WriteLine("No valid synchronization option selected, exiting...");
                    break;
            }
            WriteLine($"------------------------------------------------------------------------------");

            WriteLine("Cleaning up cache after running...");
            await distributedCache.RemoveAsync(cacheKey);
            WriteLine($"------------------------------------------------------------------------------");

            WriteLine("The bootstrapper has finished its work, press any key to exit...");
            ReadKey();
        }

        static void RunAsync()
        {
            WriteLine("Please enter the number of your desired benchmark:");
            WriteLine("1. Using in-memory references with a semaphore lock");
            WriteLine("2. Using Redis without locking");
            WriteLine("3. Using Redis with a semaphore lock");
            WriteLine("4. Using Redis with Redlock.net");
            WriteLine($"------------------------------------------------------------------------------");
            if (!int.TryParse(ReadLine(), out var parsedBenchmarkNumber) || (parsedBenchmarkNumber < 1 && parsedBenchmarkNumber > 4))
            {
                WriteLine("You have selected an invalid number, please restart the program");
                return;
            }
            WriteLine($"You have selected option {parsedBenchmarkNumber}");
            WriteLine($"------------------------------------------------------------------------------");
            WriteLine("Please enter the number of instances you would like to run this option with:");
            if (!int.TryParse(ReadLine(), out var parsedInstanceAmount) || parsedBenchmarkNumber <= 0)
            {
                WriteLine("You have selected an invalid number, please restart the program");
                return;
            }
            WriteLine($"------------------------------------------------------------------------------");
            WriteLine("Commencing benchmarks...");

            Parallel.For(0, parsedInstanceAmount, _ =>
            {
                var applicationProcess = new Process();
                var applicationProjectFolder = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                                                                            @"..\..\..\..\DCP.Application.Async"));
                var processInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = $"run -p DCP.Application.Async.csproj {parsedBenchmarkNumber}",
                    WorkingDirectory = applicationProjectFolder,
                    UseShellExecute = true
                };
                applicationProcess.StartInfo = processInfo;
                applicationProcess.Start();
                applicationProcess.WaitForExit();
            });
        }

        static void RunSync()
        {
            WriteLine("Not implemented yet, exiting...");
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
