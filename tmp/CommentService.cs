using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Cache.Console
{
    public class CommentService
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public CommentService(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public async Task Execute()
        {
            Console.WriteLine("Retrieving comments...");
            var comments = await GetCommentsFromOrigin();
            if (comments is null || !comments.Any())
                throw new Exception("No comments found!");

            Console.WriteLine($"Retrieved {comments.Count()} comments!");
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
    }
}
