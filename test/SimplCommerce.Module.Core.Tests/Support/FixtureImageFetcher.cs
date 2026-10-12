using System.Net.Http;
using System.Threading.Tasks;

namespace SimplCommerce.Module.Core.Tests.Support
{
    public class FixtureImageFetcher
    {
        private readonly HttpClient _httpClient;

        public FixtureImageFetcher(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<byte[]> FetchAsync(string imageUrl)
        {
            return await _httpClient.GetByteArrayAsync(imageUrl);
        }
    }
}
