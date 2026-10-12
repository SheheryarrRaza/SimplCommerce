using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace SimplCommerce.Module.PaymentMollie.Services
{
    public class MollieClient : IMollieClient
    {
        private const string PaymentsUrl = "https://api.mollie.com/v2/payments";

        private readonly IHttpClientFactory _httpClientFactory;

        public MollieClient(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public async Task<MolliePaymentResponse> CreatePayment(string apiKey, decimal amount, string currency, string description, string redirectUrl, string webhookUrl, Dictionary<string, string> metadata)
        {
            var request = new MollieCreatePaymentRequest
            {
                // Mollie requires the amount as a plain decimal string with exactly two digits,
                // formatted with the invariant culture regardless of the store's currency culture.
                Amount = new MollieAmount { Currency = currency, Value = amount.ToString("F2", CultureInfo.InvariantCulture) },
                Description = description,
                RedirectUrl = redirectUrl,
                WebhookUrl = webhookUrl,
                Metadata = metadata
            };

            var httpClient = CreateClient(apiKey);
            var content = new StringContent(JsonConvert.SerializeObject(request, CreateSerializerSettings()), Encoding.UTF8, "application/json");
            var response = await httpClient.PostAsync(PaymentsUrl, content);
            response.EnsureSuccessStatusCode();
            var responseBody = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<MolliePaymentResponse>(responseBody, CreateSerializerSettings());
        }

        public async Task<MolliePaymentResponse> GetPayment(string apiKey, string paymentId)
        {
            var httpClient = CreateClient(apiKey);
            var response = await httpClient.GetAsync($"{PaymentsUrl}/{paymentId}");
            response.EnsureSuccessStatusCode();
            var responseBody = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<MolliePaymentResponse>(responseBody, CreateSerializerSettings());
        }

        private HttpClient CreateClient(string apiKey)
        {
            var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            return httpClient;
        }

        private static JsonSerializerSettings CreateSerializerSettings()
        {
            return new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            };
        }
    }
}
