using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SimplCommerce.Infrastructure.Data;
using SimplCommerce.Module.Catalog.Models;
using SimplCommerce.Module.Core.Services;

namespace SimplCommerce.Module.Catalog.Areas.Catalog.Controllers
{
    [Area("Catalog")]
    [Authorize(Roles = "admin, vendor")]
    [Route("api/products/{productId}/media")]
    public class ProductMediaApiController : Controller
    {
        private readonly IRepository<Product> _productRepository;
        private readonly IMediaService _mediaService;
        private readonly IWebHostEnvironment _hostingEnvironment;
        private readonly IHttpClientFactory _httpClientFactory;

        public ProductMediaApiController(IRepository<Product> productRepository, IMediaService mediaService, IWebHostEnvironment hostingEnvironment, IHttpClientFactory httpClientFactory)
        {
            _productRepository = productRepository;
            _mediaService = mediaService;
            _hostingEnvironment = hostingEnvironment;
            _httpClientFactory = httpClientFactory;
        }

        [HttpPost("import")]
        public async Task<IActionResult> ImportImage(long productId, [FromBody] ImportImageRequest request)
        {
            var product = _productRepository.Query().FirstOrDefault(x => x.Id == productId);
            if (product == null)
            {
                return NotFound();
            }

            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(20);
            var response = await client.GetAsync(request.Url);
            if (!response.IsSuccessStatusCode)
            {
                return BadRequest(new { error = "The image could not be downloaded" });
            }

            var extension = Path.GetExtension(new Uri(request.Url).AbsolutePath);
            var fileName = $"{Guid.NewGuid()}{extension}";
            using (var stream = await response.Content.ReadAsStreamAsync())
            {
                await _mediaService.SaveMediaAsync(stream, fileName, response.Content.Headers.ContentType?.MediaType);
            }

            return Ok(new { fileName, url = _mediaService.GetMediaUrl(fileName) });
        }

        [HttpPost("attachments")]
        [DisableRequestSizeLimit]
        [RequestFormLimits(MultipartBodyLengthLimit = long.MaxValue)]
        public async Task<IActionResult> AddAttachment(long productId, IFormFile file)
        {
            var product = _productRepository.Query().FirstOrDefault(x => x.Id == productId);
            if (product == null)
            {
                return NotFound();
            }

            var folder = Path.Combine(_hostingEnvironment.WebRootPath, "user-content", "attachments", productId.ToString());
            Directory.CreateDirectory(folder);
            var target = Path.Combine(folder, Path.GetFileName(file.FileName));
            using (var stream = new FileStream(target, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return Ok(new { name = file.FileName, size = file.Length });
        }

        public class ImportImageRequest
        {
            public string Url { get; set; }
        }
    }
}
