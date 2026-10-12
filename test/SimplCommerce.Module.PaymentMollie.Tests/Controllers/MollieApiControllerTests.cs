using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Newtonsoft.Json;
using SimplCommerce.Infrastructure.Data;
using SimplCommerce.Module.PaymentMollie.Areas.PaymentMollie.Controllers;
using SimplCommerce.Module.PaymentMollie.ViewModels;
using SimplCommerce.Module.Payments.Models;
using Xunit;

namespace SimplCommerce.Module.PaymentMollie.Tests.Controllers
{
    public class MollieApiControllerTests
    {
        private static PaymentProvider CreateProvider(string additionalSettings)
        {
            return new PaymentProvider("MolliePayment")
            {
                Name = "Mollie",
                IsEnabled = false,
                AdditionalSettings = additionalSettings
            };
        }

        [Fact]
        public void Config_Get_ReturnsDeserializedSettings()
        {
            // Arrange
            var provider = CreateProvider("{\"ApiKey\":\"test_dummykey\",\"PaymentFee\":1.5}");
            var repositoryMock = new Mock<IRepositoryWithTypedId<PaymentProvider, string>>();
            repositoryMock.Setup(r => r.Query()).Returns(new List<PaymentProvider> { provider }.AsQueryable());
            var controller = new MollieApiController(repositoryMock.Object);

            // Act
            var result = controller.Config();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var model = Assert.IsType<MollieConfigForm>(okResult.Value);
            Assert.Equal("test_dummykey", model.ApiKey);
            Assert.Equal(1.5m, model.PaymentFee);
        }

        [Fact]
        public async Task Config_Put_SavesSettings_WhenModelStateIsValid()
        {
            // Arrange
            var provider = CreateProvider("{}");
            var repositoryMock = new Mock<IRepositoryWithTypedId<PaymentProvider, string>>();
            repositoryMock.Setup(r => r.Query()).Returns(new List<PaymentProvider> { provider }.AsQueryable());
            var controller = new MollieApiController(repositoryMock.Object);
            var model = new MollieConfigForm { ApiKey = "test_dummykey", PaymentFee = 2 };

            // Act
            var result = await controller.Config(model);

            // Assert
            Assert.IsType<AcceptedResult>(result);
            var savedSettings = JsonConvert.DeserializeObject<MollieConfigForm>(provider.AdditionalSettings);
            Assert.Equal("test_dummykey", savedSettings.ApiKey);
            Assert.Equal(2m, savedSettings.PaymentFee);
            repositoryMock.Verify(r => r.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task Config_Put_ReturnsBadRequest_WhenModelStateIsInvalid()
        {
            // Arrange
            var provider = CreateProvider("{}");
            var repositoryMock = new Mock<IRepositoryWithTypedId<PaymentProvider, string>>();
            repositoryMock.Setup(r => r.Query()).Returns(new List<PaymentProvider> { provider }.AsQueryable());
            var controller = new MollieApiController(repositoryMock.Object);
            controller.ModelState.AddModelError("ApiKey", "The ApiKey field is required.");
            var model = new MollieConfigForm { ApiKey = null, PaymentFee = 2 };

            // Act
            var result = await controller.Config(model);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
            repositoryMock.Verify(r => r.SaveChangesAsync(), Times.Never);
        }
    }
}
