using DCP.Logic;
using System.Linq;
using static System.Console;

namespace DCP.Application
{
    public static class ResultsPrinter
    {
        public static void PrintResults(ExecutionResult result)
        {
            WriteLine($"------------------------------------------------------------------------------");
            WriteLine($"| --                         Results overview                              -- |");
            WriteLine($"------------------------------------------------------------------------------");
            WriteLine($"|                                 -- --                                       |");
            WriteLine($"|                           -- Introduction --                                |");
            WriteLine($"|                                 -- --                                       |");
            WriteLine($"| This overview presents data about accessing an unlocked resource in Redis   |");
            WriteLine($"| with multiple threads.                                                      |");
            WriteLine($"|                                                                             |");
            WriteLine($"| Every section providers the amount of requests going to the origin          |");
            WriteLine($"| and to the cache.                                                           |");
            WriteLine($"|                                                                             |");
            WriteLine($"| 200 threads are executed in parallel, to simulate multiple threads          |");
            WriteLine($"| accessing the same resource at the same time.                               |");
            WriteLine($"|                                                                             |");
            WriteLine($"| The elapsed time is presented at the end of the section.                    |");
            WriteLine($"------------------------------------------------------------------------------");
            WriteLine($"|                                 -- --                                       |");
            WriteLine($"|                             -- Results --                                   |");
            WriteLine($"|                                 -- --                                       |");
            WriteLine($"| {result.ResultTitle}");
            WriteLine($"| Total amount of requests: {result.ThreadExecutionResults.Count()}");
            WriteLine($"| Requests to origin: {result.ThreadExecutionResults.Count(result => !result.GotResultFromCache)}");
            WriteLine($"| Requests to cache or memory references: {result.ThreadExecutionResults.Count(result => result.GotResultFromCache)}");
            WriteLine($"| Elapsed miliseconds: {result.ElapsedMilliseconds} ms");
            WriteLine($"|                                 -- --                                       |");
            WriteLine($"------------------------------------------------------------------------------");
        }
    }
}
