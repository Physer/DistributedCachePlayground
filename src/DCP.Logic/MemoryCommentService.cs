using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DCP.Logic
{
    public class MemoryCommentService
    {
        private readonly CommentsRepository _commentsRepository;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        public MemoryCommentService(CommentsRepository commentsRepository)
        {
            _commentsRepository = commentsRepository;
        }

        private IEnumerable<Comment> _comments;
        public async Task<ThreadExecutionResult> Execute()
        {
            await _semaphore.WaitAsync();
            try
            {
                var fromCache = false;
                if (_comments is null || !_comments.Any())
                    _comments = await _commentsRepository.GetComments();
                else
                    fromCache = true;

                return new ThreadExecutionResult
                {
                    GotResultFromCache = fromCache
                };
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }
}
