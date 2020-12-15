using System.Collections.Generic;

namespace DCP.Logic
{
    public class ExecutionResult
    {
        public IEnumerable<ThreadExecutionResult> ThreadExecutionResults { get; set; }
        public long ElapsedMilliseconds { get; set; }
        public string ResultTitle { get; set; }
    }
}
