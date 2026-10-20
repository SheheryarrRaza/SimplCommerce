using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using MockQueryable.Moq;
using Moq;
using SimplCommerce.Infrastructure.Data;
using SimplCommerce.Module.Orders.Models;
using SimplCommerce.Module.PaymentMollie.Services;
using SimplCommerce.Module.Payments.Models;
using Xunit;

namespace SimplCommerce.Module.PaymentMollie.Tests
{
    public class MolliePaymentConfirmerTests
    {
        private class TestOrder : Order
        {
            public TestOrder(long id)
            {
                Id = id;
            }
        }

        private const string MolliePaymentId = "tr_abc123";

        private readonly string _apiKey = "test_" + Guid.NewGuid().ToString("N");
        private readonly Mock<IMollieClient> _client = new Mock<IMollieClient>();
        private readonly Mock<IRepository<Payment>> _paymentRepository = new Mock<IRepository<Payment>>();
        private readonly Order _order = new TestOrder(7) { OrderTotal = 10.50m, OrderStatus = OrderStatus.PendingPayment };
        private readonly Payment _payment;
        private readonly MolliePaymentConfirmer _confirmer;

        public MolliePaymentConfirmerTests()
        {
            _payment = new Payment
            {
                OrderId = 7,
                Amount = 10.50m,
                PaymentMethod = "Mollie",
                GatewayTransactionId = MolliePaymentId,
                Status = PaymentStatus.Pending
            };

            _paymentRepository.Setup(x => x.Query()).Returns(new List<Payment> { _payment }.BuildMock());
            _paymentRepository.Setup(x => x.SaveChangesAsync()).Returns(Task.CompletedTask);

            var orderRepository = new Mock<IRepository<Order>>();
            orderRepository.Setup(x => x.Query()).Returns(new List<Order> { _order }.BuildMock());

            var providerRepository = new Mock<IRepositoryWithTypedId<PaymentProvider, string>>();
            var provider = new PaymentProvider("Mollie") { IsEnabled = true, AdditionalSettings = "{\"ApiKey\":\"" + _apiKey + "\"}" };
            providerRepository.Setup(x => x.Query()).Returns(new List<PaymentProvider> { provider }.BuildMock());

            _confirmer = new MolliePaymentConfirmer(_client.Object, _paymentRepository.Object, orderRepository.Object, providerRepository.Object, NullLogger<MolliePaymentConfirmer>.Instance);
        }

        private void MollieReports(string status, decimal amount)
        {
            _client.Setup(x => x.GetPaymentAsync(_apiKey, MolliePaymentId))
                .ReturnsAsync(new MolliePaymentInfo { Id = MolliePaymentId, Status = status, Amount = amount, Currency = "EUR" });
        }

        [Fact]
        public async Task Paid_WithMatchingAmount_MarksPaymentSucceededAndOrderPaid()
        {
            MollieReports("paid", 10.50m);

            var result = await _confirmer.ConfirmAsync(MolliePaymentId);

            Assert.Same(_payment, result);
            Assert.Equal(PaymentStatus.Succeeded, _payment.Status);
            Assert.Equal(OrderStatus.PaymentReceived, _order.OrderStatus);
            _paymentRepository.Verify(x => x.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task Paid_WithMismatchedAmount_ChangesNothing()
        {
            MollieReports("paid", 0.01m);

            await _confirmer.ConfirmAsync(MolliePaymentId);

            Assert.Equal(PaymentStatus.Pending, _payment.Status);
            Assert.Equal(OrderStatus.PendingPayment, _order.OrderStatus);
            _paymentRepository.Verify(x => x.SaveChangesAsync(), Times.Never);
        }

        [Theory]
        [InlineData("failed")]
        [InlineData("canceled")]
        [InlineData("expired")]
        public async Task FailedCanceledExpired_MarkPaymentFailedAndOrderPaymentFailed(string status)
        {
            MollieReports(status, 10.50m);

            await _confirmer.ConfirmAsync(MolliePaymentId);

            Assert.Equal(PaymentStatus.Failed, _payment.Status);
            Assert.Contains(status, _payment.FailureMessage);
            Assert.Equal(OrderStatus.PaymentFailed, _order.OrderStatus);
        }

        [Theory]
        [InlineData("open")]
        [InlineData("pending")]
        [InlineData("authorized")]
        public async Task OpenPayment_ChangesNothing(string status)
        {
            MollieReports(status, 10.50m);

            await _confirmer.ConfirmAsync(MolliePaymentId);

            Assert.Equal(PaymentStatus.Pending, _payment.Status);
            Assert.Equal(OrderStatus.PendingPayment, _order.OrderStatus);
            _paymentRepository.Verify(x => x.SaveChangesAsync(), Times.Never);
        }

        [Fact]
        public async Task RepeatedConfirm_OnFinalPayment_DoesNothing()
        {
            MollieReports("paid", 10.50m);
            await _confirmer.ConfirmAsync(MolliePaymentId);
            _client.Invocations.Clear();
            _paymentRepository.Invocations.Clear();

            // A later (even contradicting) report must not alter the finalised payment or order.
            MollieReports("failed", 10.50m);
            await _confirmer.ConfirmAsync(MolliePaymentId);

            Assert.Equal(PaymentStatus.Succeeded, _payment.Status);
            Assert.Equal(OrderStatus.PaymentReceived, _order.OrderStatus);
            _client.Verify(x => x.GetPaymentAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            _paymentRepository.Verify(x => x.SaveChangesAsync(), Times.Never);
        }

        [Fact]
        public async Task OrderNoLongerPendingPayment_IsNotChanged()
        {
            _order.OrderStatus = OrderStatus.Canceled;
            MollieReports("paid", 10.50m);

            await _confirmer.ConfirmAsync(MolliePaymentId);

            Assert.Equal(PaymentStatus.Pending, _payment.Status);
            Assert.Equal(OrderStatus.Canceled, _order.OrderStatus);
        }

        [Theory]
        [InlineData("tr_unknown1")]
        [InlineData("../x")]
        [InlineData(null)]
        public async Task UnknownOrMalformedId_ReturnsNullWithoutCallingMollie(string id)
        {
            var result = await _confirmer.ConfirmAsync(id);

            Assert.Null(result);
            _client.Verify(x => x.GetPaymentAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task MollieFailure_PropagatesAndChangesNothing()
        {
            _client.Setup(x => x.GetPaymentAsync(_apiKey, MolliePaymentId)).ThrowsAsync(new MollieException("Could not reach Mollie."));

            await Assert.ThrowsAsync<MollieException>(() => _confirmer.ConfirmAsync(MolliePaymentId));

            Assert.Equal(PaymentStatus.Pending, _payment.Status);
            Assert.Equal(OrderStatus.PendingPayment, _order.OrderStatus);
        }
    }
}
