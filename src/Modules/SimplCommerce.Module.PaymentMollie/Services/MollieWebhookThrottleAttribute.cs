using System;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace SimplCommerce.Module.PaymentMollie.Services
{
    /// <summary>
    /// Per-client fixed-window throttle for the anonymous Mollie webhook, so callers cannot make the
    /// server call out to Mollie without limit.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class MollieWebhookThrottleAttribute : Attribute, IAsyncActionFilter
    {
        public const int PermitsPerMinute = 30;

        private static readonly PartitionedRateLimiter<string> Limiter = PartitionedRateLimiter.Create<string, string>(
            key => RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = PermitsPerMinute,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var key = context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            using var lease = await Limiter.AcquireAsync(key);
            if (!lease.IsAcquired)
            {
                context.Result = new StatusCodeResult(StatusCodes.Status429TooManyRequests);
                return;
            }

            await next();
        }
    }
}
