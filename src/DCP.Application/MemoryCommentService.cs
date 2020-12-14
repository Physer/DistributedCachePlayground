using System.Collections.Generic;

namespace DCP.Application
{
    public class MemoryCommentService
    {
        public ThreadExecutionResult Execute(IEnumerable<Comment> comments) => new ThreadExecutionResult
        {
            GotResultFromCache = true
        };
    }
}
