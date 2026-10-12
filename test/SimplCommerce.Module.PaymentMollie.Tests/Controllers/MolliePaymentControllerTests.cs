using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SimplCommerce.Infrastructure.Data;
using SimplCommerce.Module.Checkouts.Services;
using SimplCommerce.Module.Core.Extensions;
using SimplCommerce.Module.Core.Models;
using SimplCommerce.Module.Core.Services;
using SimplCommerce.Module.Orders.Models;
using SimplCommerce.Module.Orders.Services;
using SimplCommerce.Module.PaymentMollie.Areas.PaymentMollie.Controllers;
using SimplCommerce.Module.PaymentMollie.Services;
using SimplCommerce.Module.Payments.Models;
using Xunit;

namespace SimplCommerce.Module.PaymentMollie.Tests.Controllers
{
    public class MolliePaymentControllerTests
    {
        private const long CurrentUserId = 42;
        private const string MollieTransactionId = "tr_test_123";

        // Order/Payment.Id has a protected setter (set by EF on insert), so tests set it via reflection.
        private static void SetId<T>(T entity, long id)
        {
            typeof(T).GetProperty("Id", BindingFlags.Public | BindingFlags.Instance).SetValue(entity, id);
        }

        private static MolliePaymentController CreateController(
            Order order,
            Payment payment,
            Mock<IMollieClient> mollieClientMock,
            out Mock<IRepository<Payment>> paymentRepositoryMock)
        {
            var orderRepositoryMock = new Mock<IRepository<Order>>();
            orderRepositoryMock.Setup(r => r.Query()).Returns(new List<Order> { order }.AsQueryable());

            paymentRepositoryMock = new Mock<IRepository<Payment>>();
            var payments = payment == null ? new List<Payment>() : new List<Payment> { payment };
            paymentRepositoryMock.Setup(r => r.Query()).Returns(payments.AsQueryable());

            var provider = new PaymentProvider("MolliePayment")
            {
                Name = "Mollie",
                IsEnabled = true,
                AdditionalSettings = "{\"ApiKey\":\"test_dummykey\",\"PaymentFee\":0}"
            };
            var providerRepositoryMock = new Mock<IRepositoryWithTypedId<PaymentProvider, string>>();
            providerRepositoryMock.Setup(r => r.Query()).Returns(new List<PaymentProvider> { provider }.AsQueryable());

            var workContextMock = new Mock<IWorkContext>();
            workContextMock.Setup(w => w.GetCurrentUser()).ReturnsAsync(new User { Id = CurrentUserId });

            var controller = new MolliePaymentController(
                Mock.Of<IOrderService>(),
                orderRepositoryMock.Object,
                paymentRepositoryMock.Object,
                Mock.Of<ICheckoutService>(),
                workContextMock.Object,
                providerRepositoryMock.Object,
                mollieClientMock.Object,
                Mock.Of<ICurrencyService>());

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };

            return controller;
        }

        [Fact]
        public async Task Return_MarksOrderPaymentReceived_WhenMollieConfirmsPaid()
        {
            // Arrange
            var order = new Order { CreatedById = CurrentUserId, OrderStatus = OrderStatus.PendingPayment };
            SetId(order, 1);
            var payment = new Payment
            {
                OrderId = 1,
                PaymentMethod = "MolliePayment",
                Status = PaymentStatus.Pending,
                GatewayTransactionId = MollieTransactionId,
                CreatedOn = DateTimeOffset.UtcNow
            };
            SetId(payment, 1);
            var mollieClientMock = new Mock<IMollieClient>();
            mollieClientMock.Setup(c => c.GetPayment("test_dummykey", MollieTransactionId))
                .ReturnsAsync(new MolliePaymentResponse { Id = MollieTransactionId, Status = "paid" });

            var controller = CreateController(order, payment, mollieClientMock, out var paymentRepositoryMock);

            // Act
            var result = await controller.Return(order.Id);

            // Assert
            Assert.Equal(OrderStatus.PaymentReceived, order.OrderStatus);
            Assert.Equal(PaymentStatus.Succeeded, payment.Status);
            var redirectResult = Assert.IsType<RedirectResult>(result);
            Assert.Contains("checkout/success", redirectResult.Url);
            paymentRepositoryMock.Verify(r => r.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task Return_DoesNotMarkOrderPaid_WhenMollieStatusIsNotPaid_EvenIfQueryStringClaimsSuccess()
        {
            // Arrange
            var order = new Order { CreatedById = CurrentUserId, OrderStatus = OrderStatus.PendingPayment };
            SetId(order, 2);
            var payment = new Payment
            {
                OrderId = 2,
                PaymentMethod = "MolliePayment",
                Status = PaymentStatus.Pending,
                GatewayTransactionId = MollieTransactionId,
                CreatedOn = DateTimeOffset.UtcNow
            };
            SetId(payment, 2);
            var mollieClientMock = new Mock<IMollieClient>();
            // Mollie itself reports the payment failed, regardless of what the browser's query string says.
            mollieClientMock.Setup(c => c.GetPayment("test_dummykey", MollieTransactionId))
                .ReturnsAsync(new MolliePaymentResponse { Id = MollieTransactionId, Status = "failed" });

            var controller = CreateController(order, payment, mollieClientMock, out var paymentRepositoryMock);
            // A forged return URL that claims success in the query string; the action takes only orderId
            // as a parameter and must not be swayed by this.
            controller.ControllerContext.HttpContext.Request.QueryString = new QueryString("?orderId=2&status=success&paid=true");

            // Act
            var result = await controller.Return(order.Id);

            // Assert
            Assert.Equal(OrderStatus.PaymentFailed, order.OrderStatus);
            Assert.Equal(PaymentStatus.Failed, payment.Status);
            var redirectResult = Assert.IsType<RedirectResult>(result);
            Assert.Contains("checkout/error", redirectResult.Url);
        }

        [Fact]
        public async Task Webhook_IsIdempotent_WhenCalledAgainForAlreadySettledOrder()
        {
            // Arrange: the browser return already confirmed the payment as succeeded.
            var order = new Order { CreatedById = CurrentUserId, OrderStatus = OrderStatus.PaymentReceived };
            SetId(order, 3);
            var payment = new Payment
            {
                OrderId = 3,
                PaymentMethod = "MolliePayment",
                Status = PaymentStatus.Succeeded,
                GatewayTransactionId = MollieTransactionId,
                CreatedOn = DateTimeOffset.UtcNow
            };
            SetId(payment, 3);
            var mollieClientMock = new Mock<IMollieClient>();

            var controller = CreateController(order, payment, mollieClientMock, out var paymentRepositoryMock);

            // Act
            var result = await controller.Webhook(MollieTransactionId);

            // Assert
            Assert.IsType<AcceptedResult>(result);
            Assert.Equal(OrderStatus.PaymentReceived, order.OrderStatus);
            Assert.Equal(PaymentStatus.Succeeded, payment.Status);
            mollieClientMock.Verify(c => c.GetPayment(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            paymentRepositoryMock.Verify(r => r.SaveChangesAsync(), Times.Never);
        }
    }
}
