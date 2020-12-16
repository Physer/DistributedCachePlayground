using Microsoft.Extensions.Caching.Distributed;
using Newtonsoft.Json;
using StackExchange.Redis;
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
        //private readonly IDistributedCache _distributedCache;
        private readonly CommentsRepository _commentsRepository;

        private readonly SemaphoreSlim _semaphoreLock = new SemaphoreSlim(1, 1);

        private const string _cacheKey = "comments";

        public CachedCommentService(//IDistributedCache distributedCache,
            CommentsRepository commentsRepository)
        {
            //_distributedCache = distributedCache;
            _commentsRepository = commentsRepository;
        }

        public async Task<ThreadExecutionResult> Execute(IDatabase cache, LockType lockType, bool alwaysUseOrigin = false)
        {
            if (alwaysUseOrigin)
                return await ExecuteFromOrigin();

            return lockType switch
            {
                LockType.Redlock => await ExecuteWithRedlock(),
                LockType.Semaphore => await ExecuteWithSemaphore(cache),
                LockType.None => await ExecuteWithoutLock(),
                _ => throw new Exception("No valid lock type found!"),
            };
        }

        private async Task<ThreadExecutionResult> ExecuteFromOrigin()
        {
            _ = await _commentsRepository.GetComments();
            return new ThreadExecutionResult
            {
                GotResultFromCache = false
            };
        }

        private async Task<ThreadExecutionResult> ExecuteWithoutLock() => await Execute(null);

        private async Task<ThreadExecutionResult> ExecuteWithSemaphore(IDatabase cache)
        {
            await _semaphoreLock.WaitAsync();
            try
            {
                return await Execute(cache);
            }
            finally
            {
                _semaphoreLock.Release();
            }
        }

        private async Task<ThreadExecutionResult> ExecuteWithRedlock()
        {
            var lockKey = "comments-lock";
            var expiry = TimeSpan.FromSeconds(30);
            var wait = TimeSpan.FromSeconds(10);
            var retry = TimeSpan.FromSeconds(1);

            using var redlock = await InitializedRedlockFactory.Instance.Factory.CreateLockAsync(lockKey, expiry, wait, retry);
            if (redlock.IsAcquired)
                return await Execute(null);

            return new ThreadExecutionResult();
        }

        private async Task<ThreadExecutionResult> Execute(IDatabase cache)
        {
            if (cache is null)
                throw new NotImplementedException();

            var fromCache = false;
            var comments = await GetCommentsFromCache(cache);
            if (comments is null || !comments.Any())
            {
                comments = await _commentsRepository.GetComments();
                if (comments is null || !comments.Any())
                    throw new Exception("No comments found!");
                await SetCommentsInCache(cache, comments);
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

        private async Task<IEnumerable<Comment>> GetCommentsFromCache(IDatabase cache)
        {
            if (cache is null)
                throw new NotImplementedException();

            var cacheDataString = await cache.StringGetAsync(_cacheKey);
            //var cacheData = await _distributedCache.GetAsync(_cacheKey);
            //if (cacheData is null || !cacheData.Any())
            //    return null;

            //var cacheDataString = Encoding.UTF8.GetString(cacheData);
            if (string.IsNullOrWhiteSpace(cacheDataString))
                return null;

            var deserializedComments = JsonConvert.DeserializeObject<IEnumerable<Comment>>(cacheDataString);
            if (deserializedComments is null || !deserializedComments.Any())
                return null;

            return deserializedComments;
        }

        private async Task SetCommentsInCache(IDatabase cache, IEnumerable<Comment> comments)
        {
            if (cache is null)
                throw new NotImplementedException();
            
            var serializedComments = JsonConvert.SerializeObject(comments);
            if (string.IsNullOrWhiteSpace(serializedComments))
                return;

            await cache.StringSetAsync(_cacheKey, serializedComments);

            //await _distributedCache.SetAsync(_cacheKey, Encoding.UTF8.GetBytes(serializedComments));
        }
    }
}
