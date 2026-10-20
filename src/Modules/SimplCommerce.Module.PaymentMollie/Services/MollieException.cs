using System;

namespace SimplCommerce.Module.PaymentMollie.Services
{
    /// <summary>
    /// Raised when a call to Mollie fails. The message is always safe to show or log:
    /// it never contains the API key or the body of Mollie's response.
    /// </summary>
    public class MollieException : Exception
    {
        public MollieException(string message) : base(message)
        {
        }
    }
}
