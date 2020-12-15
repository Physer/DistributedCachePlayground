﻿using DCP.Logic;
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
        static void Main(string[] args)
        {
            WriteLine("Distributed Cache Playground | Bootstrapper");
            WriteLine($"------------------------------------------------------------------------------");

            using IHost host = CreateHostBuilder(args).Build();

            WriteLine("Please enter the number of your desired benchmark:");
            WriteLine("1. Using in-memory references");
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
                                                                            @"..\..\..\..\DCP.Application"));
                var processInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = "run -p DCP.Application.csproj",
                    WorkingDirectory = applicationProjectFolder,
                    UseShellExecute = true
                };
                applicationProcess.StartInfo = processInfo;
                applicationProcess.Start();
            });            

            WriteLine("The bootstrapper has finished its work, press any key to exit...");
            ReadKey();
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
