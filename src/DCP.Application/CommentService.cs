using Microsoft.Extensions.Caching.Distributed;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DCP.Application
{
    public class CommentService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IDistributedCache _distributedCache;
        private readonly SemaphoreSlim _semaphoreLock = new SemaphoreSlim(1, 1);

        private const string _cacheKey = "comments";

        public CommentService(IHttpClientFactory httpClientFactory,
            IDistributedCache distributedCache)
        {
            _httpClientFactory = httpClientFactory;
            _distributedCache = distributedCache;
        }

        public async Task<ThreadExecutionResult> Execute(LockType lockType, bool alwaysUseOrigin = false)
        {
            if (alwaysUseOrigin)
                return await ExecuteFromOrigin();

            return lockType switch
            {
                LockType.Redlock => await ExecuteWithRedlock(),
                LockType.Semaphore => await ExecuteWithSemaphore(),
                LockType.None => await ExecuteWithoutLock(),
                _ => throw new Exception("No valid lock type found!"),
            };
        }

        private async Task<ThreadExecutionResult> ExecuteFromOrigin()
        {
            var results = await GetCommentsFromOrigin();
            return new ThreadExecutionResult
            {
                GotResultFromCache = false
            };
        }

        private async Task<ThreadExecutionResult> ExecuteWithoutLock() => await Execute();

        private async Task<ThreadExecutionResult> ExecuteWithSemaphore()
        {
            await _semaphoreLock.WaitAsync();
            try
            {
                return await Execute();
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
                return await Execute();

            return new ThreadExecutionResult();
        }

        private async Task<ThreadExecutionResult> Execute()
        {
            var fromCache = false;
            Console.WriteLine("Retrieving comments...");
            var comments = await GetCommentsFromCache();
            if (comments is null || !comments.Any())
            {
                Console.WriteLine("No comments found in Redis, proceeding to origin");
                comments = await GetCommentsFromOrigin();
                if (comments is null || !comments.Any())
                    throw new Exception("No comments found!");
                Console.WriteLine("Putting comments in Redis...");
                await SetCommentsInCache(comments);
            }
            else
                fromCache = true;
            if (comments is null || !comments.Any())
                throw new Exception("No comments found!");

            Console.WriteLine($"Retrieved {comments.Count()} comments!");
            return new ThreadExecutionResult
            {
                GotResultFromCache = fromCache
            };
        }

        private async Task<IEnumerable<Comment>> GetCommentsFromOrigin()
        {
            Console.WriteLine("Retrieving from origin...");
            var httpClient = _httpClientFactory.CreateClient();
            httpClient.BaseAddress = new Uri("https://jsonplaceholder.typicode.com");
            var request = new HttpRequestMessage(HttpMethod.Get, "/comments");
            var response = await httpClient.SendAsync(request);
            return JsonConvert.DeserializeObject<IEnumerable<Comment>>(await response.Content.ReadAsStringAsync());
        }

        private async Task<IEnumerable<Comment>> GetCommentsFromCache()
        {
            Console.WriteLine("Retrieving from Redis...");
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

        private async Task SetCommentsInCache(IEnumerable<Comment> comments)
        {
            var serializedComments = JsonConvert.SerializeObject(comments);
            await _distributedCache.SetAsync(_cacheKey, Encoding.UTF8.GetBytes(serializedComments));
        }
    }
}
