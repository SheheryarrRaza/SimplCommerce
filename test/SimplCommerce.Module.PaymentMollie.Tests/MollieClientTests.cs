using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using SimplCommerce.Module.PaymentMollie.Services;
using Xunit;

namespace SimplCommerce.Module.PaymentMollie.Tests
{
    public class MollieClientTests
    {
        private const string ResponseBodyMarker = "secret-response-marker";

        private readonly string _apiKey = "test_" + Guid.NewGuid().ToString("N");

        private class StubHandler : HttpMessageHandler
        {
            private readonly HttpStatusCode _statusCode;
            private readonly string _body;

            public StubHandler(HttpStatusCode statusCode, string body)
            {
                _statusCode = statusCode;
                _body = body;
            }

            public HttpRequestMessage Request { get; private set; }

            public string RequestBody { get; private set; }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Request = request;
                RequestBody = request.Content == null ? null : await request.Content.ReadAsStringAsync();
                return new HttpResponseMessage(_statusCode) { Content = new StringContent(_body) };
            }
        }

        private static MollieClient CreateClient(StubHandler handler)
        {
            var factory = new Mock<IHttpClientFactory>();
            factory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(handler));
            return new MollieClient(factory.Object);
        }

        [Fact]
        public async Task CreatePaymentAsync_SendsExpectedRequestAndParsesResult()
        {
            var handler = new StubHandler(HttpStatusCode.Created,
                "{\"id\":\"tr_abc123\",\"status\":\"open\",\"_links\":{\"checkout\":{\"href\":\"https://www.mollie.com/checkout/abc\"}}}");

            var result = await CreateClient(handler).CreatePaymentAsync(_apiKey, "10.50", "EUR", "Order 7", "https://shop.test/mollie/return?orderId=7", "https://shop.test/mollie/webhook", 7);

            Assert.Equal("tr_abc123", result.Id);
            Assert.Equal("https://www.mollie.com/checkout/abc", result.CheckoutUrl);
            Assert.Equal(HttpMethod.Post, handler.Request.Method);
            Assert.Equal("/v2/payments", handler.Request.RequestUri.AbsolutePath);
            Assert.Equal("api.mollie.com", handler.Request.RequestUri.Host);
            Assert.Equal("Bearer", handler.Request.Headers.Authorization.Scheme);
            Assert.Equal(_apiKey, handler.Request.Headers.Authorization.Parameter);
            Assert.Contains("\"value\":\"10.50\"", handler.RequestBody);
            Assert.Contains("\"currency\":\"EUR\"", handler.RequestBody);
            Assert.Contains("\"redirectUrl\":\"https://shop.test/mollie/return?orderId=7\"", handler.RequestBody);
            Assert.Contains("\"webhookUrl\":\"https://shop.test/mollie/webhook\"", handler.RequestBody);
            Assert.Contains("\"orderId\":\"7\"", handler.RequestBody);
            Assert.DoesNotContain(_apiKey, handler.RequestBody);
        }

        [Fact]
        public async Task GetPaymentAsync_ParsesStatusAndAmount()
        {
            var handler = new StubHandler(HttpStatusCode.OK,
                "{\"id\":\"tr_abc123\",\"status\":\"paid\",\"amount\":{\"currency\":\"EUR\",\"value\":\"10.50\"}}");

            var result = await CreateClient(handler).GetPaymentAsync(_apiKey, "tr_abc123");

            Assert.Equal("paid", result.Status);
            Assert.Equal(10.50m, result.Amount);
            Assert.Equal("EUR", result.Currency);
            Assert.Equal(HttpMethod.Get, handler.Request.Method);
            Assert.Equal("/v2/payments/tr_abc123", handler.Request.RequestUri.AbsolutePath);
            Assert.Equal(_apiKey, handler.Request.Headers.Authorization.Parameter);
        }

        [Theory]
        [InlineData(HttpStatusCode.Unauthorized)]
        [InlineData(HttpStatusCode.InternalServerError)]
        [InlineData(HttpStatusCode.UnprocessableEntity)]
        public async Task Errors_ThrowMollieExceptionWithSafeMessage(HttpStatusCode statusCode)
        {
            var handler = new StubHandler(statusCode, ResponseBodyMarker + " " + _apiKey);
            var client = CreateClient(handler);

            var createError = await Assert.ThrowsAsync<MollieException>(() => client.CreatePaymentAsync(_apiKey, "1.00", "EUR", "d", "https://a.test/r", "https://a.test/w", 1));
            var getError = await Assert.ThrowsAsync<MollieException>(() => client.GetPaymentAsync(_apiKey, "tr_abc123"));

            foreach (var error in new[] { createError, getError })
            {
                Assert.DoesNotContain(_apiKey, error.Message);
                Assert.DoesNotContain(ResponseBodyMarker, error.Message);
            }
        }

        [Fact]
        public async Task GetPaymentAsync_RejectsMalformedIdWithoutCallingMollie()
        {
            var handler = new StubHandler(HttpStatusCode.OK, "{}");

            await Assert.ThrowsAsync<MollieException>(() => CreateClient(handler).GetPaymentAsync(_apiKey, "../x"));

            Assert.Null(handler.Request);
        }

        [Fact]
        public async Task GetPaymentAsync_MalformedBodyThrowsMollieException()
        {
            var handler = new StubHandler(HttpStatusCode.OK, "not json");

            await Assert.ThrowsAsync<MollieException>(() => CreateClient(handler).GetPaymentAsync(_apiKey, "tr_abc123"));
        }

        [Fact]
        public async Task MissingApiKey_ThrowsMollieExceptionWithoutCallingMollie()
        {
            var handler = new StubHandler(HttpStatusCode.OK, "{}");

            await Assert.ThrowsAsync<MollieException>(() => CreateClient(handler).GetPaymentAsync(null, "tr_abc123"));

            Assert.Null(handler.Request);
        }
    }
}
