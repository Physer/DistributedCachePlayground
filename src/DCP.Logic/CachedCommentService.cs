using Microsoft.Extensions.Caching.Distributed;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DCP.Logic
{
    public class CachedCommentService
    {
        private readonly IDistributedCache _distributedCache;
        private readonly CommentsRepository _commentsRepository;

        private readonly SemaphoreSlim _semaphoreLock = new SemaphoreSlim(1, 1);

        private const string _cacheKey = "comments";

        public CachedCommentService(IDistributedCache distributedCache,
            CommentsRepository commentsRepository)
        {
            _distributedCache = distributedCache;
            _commentsRepository = commentsRepository;
        }

        public async Task<ThreadExecutionResult> ExecuteAsync(LockType lockType, bool alwaysUseOrigin = false)
        {
            if (alwaysUseOrigin)
                return await ExecuteFromOriginAsync();

            return lockType switch
            {
                LockType.Redlock => await ExecuteWithRedlockAsync(),
                LockType.Semaphore => await ExecuteWithSemaphoreAsync(),
                LockType.None => await ExecuteWithoutLockAsync(),
                _ => throw new Exception("No valid lock type found!"),
            };
        }

        private async Task<ThreadExecutionResult> ExecuteFromOriginAsync()
        {
            _ = await _commentsRepository.GetCommentsAsync();
            return new ThreadExecutionResult
            {
                GotResultFromCache = false
            };
        }

        private async Task<ThreadExecutionResult> ExecuteWithoutLockAsync() => await ExecuteAsync();

        private async Task<ThreadExecutionResult> ExecuteWithSemaphoreAsync()
        {
            await _semaphoreLock.WaitAsync();
            try
            {
                return await ExecuteAsync();
            }
            finally
            {
                _semaphoreLock.Release();
            }
        }

        private async Task<ThreadExecutionResult> ExecuteWithRedlockAsync()
        {
            var lockKey = "comments-lock";
            var expiry = TimeSpan.FromSeconds(30);
            var wait = TimeSpan.FromSeconds(10);
            var retry = TimeSpan.FromSeconds(1);

            using var redlock = await InitializedRedlockFactory.Instance.Factory.CreateLockAsync(lockKey, expiry, wait, retry);
            if (redlock.IsAcquired)
                return await ExecuteAsync();

            return new ThreadExecutionResult();
        }

        private async Task<ThreadExecutionResult> ExecuteAsync()
        {
            var fromCache = false;
            var comments = await GetCommentsFromCacheAsync();
            if (comments is null || !comments.Any())
            {
                comments = await _commentsRepository.GetCommentsAsync();
                if (comments is null || !comments.Any())
                    throw new Exception("No comments found!");
                await SetCommentsInCacheAsync(comments);
            }
            else
                fromCache = true;
            if (comments is null || !comments.Any())
                throw new Exception("No comments found!");

            return new ThreadExecutionResult
            {
                GotResultFromCache = fromCache
            };
        }

        private async Task<IEnumerable<Comment>> GetCommentsFromCacheAsync()
        {
            var cacheData = await _distributedCache.GetAsync(_cacheKey);
            if (cacheData is null || !cacheData.Any())
                return null;

            var cacheDataString = Encoding.UTF8.GetString(cacheData);
            if (string.IsNullOrWhiteSpace(cacheDataString))
                return null;

            var deserializedComments = JsonConvert.DeserializeObject<IEnumerable<Comment>>(cacheDataString);
            if (deserializedComments is null || !deserializedComments.Any())
                return null;

            return deserializedComments;
        }

        private async Task SetCommentsInCacheAsync(IEnumerable<Comment> comments)
        {
            var serializedComments = JsonConvert.SerializeObject(comments);
            await _distributedCache.SetAsync(_cacheKey, Encoding.UTF8.GetBytes(serializedComments));
        }
    }
}
