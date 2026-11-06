using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using MockQueryable.Moq;
using Moq;
using Newtonsoft.Json.Linq;
using SimplCommerce.Infrastructure.Data;
using SimplCommerce.Infrastructure.Web.SmartTable;
using SimplCommerce.Module.Catalog.Areas.Catalog.Controllers;
using SimplCommerce.Module.Catalog.Areas.Catalog.ViewModels;
using SimplCommerce.Module.Catalog.Models;
using SimplCommerce.Module.Catalog.Services;
using SimplCommerce.Module.Core.Extensions;
using SimplCommerce.Module.Core.Models;
using SimplCommerce.Module.Core.Services;
using Xunit;

namespace SimplCommerce.Module.Catalog.Tests
{
    public class ProductApiControllerLowStockTests
    {
        [Fact]
        public async Task Put_SavesLowStockThresholdReturnedByGet()
        {
            var product = new Product { Name = "Sample product", Slug = "sample-product" };
            typeof(Product).GetProperty(nameof(Product.Id)).SetValue(product, 1L);
            var productRepository = new Mock<IRepository<Product>>();
            productRepository.Setup(repository => repository.Query()).Returns(new[] { product }.BuildMock());
            var workContext = new Mock<IWorkContext>();
            workContext.Setup(context => context.GetCurrentUser()).ReturnsAsync(new User());
            var productService = new Mock<IProductService>();
            var controller = CreateController(productRepository.Object, workContext.Object, productService.Object);
            var model = new ProductForm
            {
                Product = new ProductVm
                {
                    Name = product.Name,
                    Slug = product.Slug,
                    LowStockThreshold = 5
                }
            };

            var updateResult = await controller.Put(product.Id, model);
            var getResult = await controller.Get(product.Id);
            var productVm = Assert.IsType<ProductVm>(Assert.IsType<JsonResult>(getResult).Value);

            Assert.IsType<AcceptedResult>(updateResult);
            Assert.Equal(5, product.LowStockThreshold);
            Assert.Equal(5, productVm.LowStockThreshold);
            productService.Verify(service => service.Update(It.Is<Product>(updated => updated.LowStockThreshold == 5)), Times.Once);
        }

        [Fact]
        public async Task List_FiltersLowStockAcrossPagesAndCountsOnlyMatches()
        {
            var products = new[]
            {
                new Product { Name = "At threshold", StockQuantity = 5, LowStockThreshold = 5, StockTrackingIsEnabled = true },
                new Product { Name = "One above", StockQuantity = 6, LowStockThreshold = 5, StockTrackingIsEnabled = true },
                new Product { Name = "Tracking disabled", StockQuantity = 0, LowStockThreshold = 5, StockTrackingIsEnabled = false },
                new Product { Name = "No threshold", StockQuantity = 0, StockTrackingIsEnabled = true },
                new Product { Name = "Zero stock", StockQuantity = 0, LowStockThreshold = 0, StockTrackingIsEnabled = true }
            };
            for (var index = 0; index < products.Length; index++)
            {
                typeof(Product).GetProperty(nameof(Product.Id)).SetValue(products[index], index + 1);
            }

            var productRepository = new Mock<IRepository<Product>>();
            productRepository.Setup(repository => repository.Query()).Returns(products.BuildMock());
            var workContext = new Mock<IWorkContext>();
            workContext.Setup(context => context.GetCurrentUser()).ReturnsAsync(new User());
            var controller = CreateController(productRepository.Object, workContext.Object);

            var filteredResult = await controller.List(CreateParam(true, 0));
            var filteredData = Assert.IsType<SmartTableResult<ProductListItem>>(Assert.IsType<JsonResult>(filteredResult).Value);
            var filteredItems = filteredData.Items.ToList();

            Assert.Equal(2, filteredData.TotalRecord);
            Assert.Equal(2, filteredData.NumberOfPages);
            var markedProduct = Assert.Single(filteredItems);
            Assert.Equal("Zero stock", markedProduct.Name);
            Assert.True(markedProduct.IsLowStock);
            Assert.Equal(0, markedProduct.LowStockThreshold);

            var secondPageResult = await controller.List(CreateParam(true, 1));
            var secondPageData = Assert.IsType<SmartTableResult<ProductListItem>>(Assert.IsType<JsonResult>(secondPageResult).Value);

            Assert.Equal(2, secondPageData.TotalRecord);
            var secondPageProduct = Assert.Single(secondPageData.Items);
            Assert.Equal("At threshold", secondPageProduct.Name);
            Assert.True(secondPageProduct.IsLowStock);

            var unfilteredResult = await controller.List(CreateParam(null, 0));
            var unfilteredData = Assert.IsType<SmartTableResult<ProductListItem>>(Assert.IsType<JsonResult>(unfilteredResult).Value);

            Assert.Equal(products.Length, unfilteredData.TotalRecord);
        }

        private static SmartTableParam CreateParam(bool? isLowStock, int start)
        {
            var search = new JObject();
            if (isLowStock.HasValue)
            {
                search.Add("IsLowStock", isLowStock.Value);
            }

            return new SmartTableParam
            {
                Pagination = new Pagination { Start = start, Number = 1 },
                Search = new Search { PredicateObject = search },
                Sort = new Sort()
            };
        }

        private static ProductApiController CreateController(
            IRepository<Product> productRepository,
            IWorkContext workContext,
            IProductService productService = null)
        {
            var controller = new ProductApiController(
                productRepository,
                Mock.Of<IMediaService>(),
                productService ?? Mock.Of<IProductService>(),
                Mock.Of<IRepository<ProductLink>>(),
                Mock.Of<IRepository<ProductCategory>>(),
                Mock.Of<IRepository<ProductOptionValue>>(),
                Mock.Of<IRepository<ProductAttributeValue>>(),
                Mock.Of<IRepository<ProductMedia>>(),
                workContext);
            var httpContext = new DefaultHttpContext();
            httpContext.Request.ContentType = "multipart/form-data";
            httpContext.Request.Form = new FormCollection(new Dictionary<string, StringValues>());
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.Role, "admin")
            }, "test"));
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            return controller;
        }
    }
}
