using System.Collections.Generic;

namespace DCP.Application
{
    public class ExecutionResult
    {
        public IEnumerable<ThreadExecutionResult> ThreadExecutionResults { get; set; }
        public long ElapsedMiliseconds { get; set; }
    }
}
