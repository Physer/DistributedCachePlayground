using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace DCP.Logic
{
    public class CommentsRepository
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public CommentsRepository(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public async Task<IEnumerable<Comment>> GetCommentsAsync()
        {
            var httpClient = _httpClientFactory.CreateClient();
            httpClient.BaseAddress = new Uri("https://jsonplaceholder.typicode.com");
            var request = new HttpRequestMessage(HttpMethod.Get, "/comments");
            var response = await httpClient.SendAsync(request);
            return JsonConvert.DeserializeObject<IEnumerable<Comment>>(await response.Content.ReadAsStringAsync());
        }

        public IEnumerable<Comment> GetComments()
        {
            var request = (HttpWebRequest)WebRequest.Create(new Uri("https://jsonplaceholder.typicode.com/comments"));
            request.Accept = "application/json";

            var response = (HttpWebResponse)request.GetResponse();
            var responseContent = string.Empty;

            using (Stream dataStream = response.GetResponseStream())
            {
                StreamReader reader = new StreamReader(dataStream);
                responseContent = reader.ReadToEnd();
            }

            response.Close();
            var deserializedResponse = JsonConvert.DeserializeObject<IEnumerable<Comment>>(responseContent);
            return deserializedResponse;
        }
    }
}
