using System.Collections.Generic;

namespace DCP.Logic
{
    public class MemoryCommentService
    {
        public ThreadExecutionResult Execute(IEnumerable<Comment> comments) => new ThreadExecutionResult
        {
            GotResultFromCache = true
        };
    }
}
