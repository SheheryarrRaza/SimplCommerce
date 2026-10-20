using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using SimplCommerce.Module.PaymentMollie.Services;
using Xunit;

namespace SimplCommerce.Module.PaymentMollie.Tests
{
    public class MollieWebhookThrottleTests
    {
        private static ActionExecutingContext NewContext(string ip)
        {
            var http = new DefaultHttpContext();
            http.Connection.RemoteIpAddress = IPAddress.Parse(ip);
            var actionContext = new ActionContext(http, new RouteData(), new ActionDescriptor());
            return new ActionExecutingContext(actionContext, new List<IFilterMetadata>(), new Dictionary<string, object>(), new object());
        }

        [Fact]
        public async Task RejectsRequestsOverTheLimitFromOneClient()
        {
            var filter = new MollieWebhookThrottleAttribute();
            var ip = "203.0.113.7";
            for (var i = 0; i < MollieWebhookThrottleAttribute.PermitsPerMinute; i++)
            {
                var ok = NewContext(ip);
                await filter.OnActionExecutionAsync(ok, () => Task.FromResult<ActionExecutedContext>(null));
                Assert.Null(ok.Result);
            }

            var blocked = NewContext(ip);
            var called = false;
            await filter.OnActionExecutionAsync(blocked, () => { called = true; return Task.FromResult<ActionExecutedContext>(null); });

            Assert.False(called);
            var result = Assert.IsType<StatusCodeResult>(blocked.Result);
            Assert.Equal(StatusCodes.Status429TooManyRequests, result.StatusCode);
        }

        [Fact]
        public async Task OtherClientsAreNotAffected()
        {
            var filter = new MollieWebhookThrottleAttribute();
            var ctx = NewContext("198.51.100.9");
            await filter.OnActionExecutionAsync(ctx, () => Task.FromResult<ActionExecutedContext>(null));
            Assert.Null(ctx.Result);
        }
    }
}
