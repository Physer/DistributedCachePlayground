using System.Collections.Generic;

namespace Cache.Console
{
    public class ExecutionResult
    {
        public IEnumerable<ThreadExecutionResult> ThreadExecutionResults { get; set; }
        public long ElapsedMiliseconds { get; set; }
    }
}
