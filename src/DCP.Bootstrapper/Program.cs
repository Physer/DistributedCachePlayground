using System;
using System.Diagnostics;
using System.IO;
using static System.Console;

namespace DCP.Bootstrapper
{
    class Program
    {
        static void Main(string[] args)
        {
            WriteLine("Distributed Cache Playground | Bootstrapper");

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

            WriteLine("The bootstrapper has finished its work, press any key to exit...");
            ReadKey();
        }
    }
}
