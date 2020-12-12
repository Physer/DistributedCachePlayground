using Microsoft.Extensions.Caching.Distributed;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Cache.Console
{
    public class CommentService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IDistributedCache _distributedCache;

        private const string _cacheKey = "comments";

        public CommentService(IHttpClientFactory httpClientFactory,
            IDistributedCache distributedCache)
        {
            _httpClientFactory = httpClientFactory;
            _distributedCache = distributedCache;
        }

        public async Task Execute()
        {
            System.Console.WriteLine("Retrieving comments...");
            var comments = await GetCommentsFromOrigin();
            System.Console.WriteLine($"Retrieved {comments.Count()} comments!");
            if (comments is null || !comments.Any())
                throw new Exception("No comments found!");
            System.Console.WriteLine("Putting comments in Redis...");
            await SetCommentsInCache(comments);
        }

        private async Task<IEnumerable<Comment>> GetCommentsFromOrigin()
        {
            System.Console.WriteLine("Retrieving from origin...");
            var httpClient = _httpClientFactory.CreateClient();
            httpClient.BaseAddress = new Uri("https://jsonplaceholder.typicode.com");
            var request = new HttpRequestMessage(HttpMethod.Get, "/comments");
            var response = await httpClient.SendAsync(request);
            return JsonConvert.DeserializeObject<IEnumerable<Comment>>(await response.Content.ReadAsStringAsync());
        }

        private async Task SetCommentsInCache(IEnumerable<Comment> comments)
        {
            var serializedComments = JsonConvert.SerializeObject(comments);
            await _distributedCache.SetAsync(_cacheKey, Encoding.UTF8.GetBytes(serializedComments));
        }
    }
}
