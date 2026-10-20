using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SimplCommerce.Module.PaymentMollie.Services
{
    public class MollieClient : IMollieClient
    {
        private static readonly Uri BaseUri = new Uri("https://api.mollie.com/v2/");
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(20);

        private readonly IHttpClientFactory _httpClientFactory;

        public MollieClient(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public async Task<MolliePaymentCreated> CreatePaymentAsync(string apiKey, string amount, string currency, string description, string redirectUrl, string webhookUrl, long orderId)
        {
            var body = JsonSerializer.Serialize(new
            {
                amount = new { currency, value = amount },
                description,
                redirectUrl,
                webhookUrl,
                metadata = new { orderId = orderId.ToString(CultureInfo.InvariantCulture) }
            });

            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(BaseUri, "payments"))
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };

            using var document = await SendAsync(apiKey, request);
            var root = document.RootElement;
            try
            {
                return new MolliePaymentCreated
                {
                    Id = root.GetProperty("id").GetString(),
                    CheckoutUrl = root.GetProperty("_links").GetProperty("checkout").GetProperty("href").GetString()
                };
            }
            catch (Exception ex) when (ex is KeyNotFoundException || ex is InvalidOperationException)
            {
                throw new MollieException("Mollie returned an unexpected response.");
            }
        }

        public async Task<MolliePaymentInfo> GetPaymentAsync(string apiKey, string paymentId)
        {
            if (!MollieHelper.IsValidPaymentId(paymentId))
            {
                throw new MollieException("Invalid Mollie payment id.");
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(BaseUri, "payments/" + paymentId));
            using var document = await SendAsync(apiKey, request);
            var root = document.RootElement;
            try
            {
                var amount = root.GetProperty("amount");
                return new MolliePaymentInfo
                {
                    Id = root.GetProperty("id").GetString(),
                    Status = root.GetProperty("status").GetString(),
                    Amount = decimal.Parse(amount.GetProperty("value").GetString(), NumberStyles.Number, CultureInfo.InvariantCulture),
                    Currency = amount.GetProperty("currency").GetString()
                };
            }
            catch (Exception ex) when (ex is KeyNotFoundException || ex is InvalidOperationException || ex is FormatException || ex is ArgumentNullException)
            {
                throw new MollieException("Mollie returned an unexpected response.");
            }
        }

        private async Task<JsonDocument> SendAsync(string apiKey, HttpRequestMessage request)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new MollieException("Mollie is not configured.");
            }

            try
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            }
            catch (FormatException)
            {
                throw new MollieException("The Mollie API key is not valid.");
            }

            var httpClient = _httpClientFactory.CreateClient();
            using var cts = new CancellationTokenSource(RequestTimeout);
            try
            {
                using var response = await httpClient.SendAsync(request, cts.Token);
                if (!response.IsSuccessStatusCode)
                {
                    var status = (int)response.StatusCode;
                    if (status == 401 || status == 403)
                    {
                        throw new MollieException("Mollie rejected the API key.");
                    }

                    if (status >= 500)
                    {
                        throw new MollieException("Mollie is temporarily unavailable.");
                    }

                    throw new MollieException("Mollie could not process the request.");
                }

                var content = await response.Content.ReadAsStringAsync(cts.Token);
                return JsonDocument.Parse(content);
            }
            catch (HttpRequestException)
            {
                throw new MollieException("Could not reach Mollie.");
            }
            catch (OperationCanceledException)
            {
                throw new MollieException("Mollie did not respond in time.");
            }
            catch (JsonException)
            {
                throw new MollieException("Mollie returned an unexpected response.");
            }
        }
    }
}
