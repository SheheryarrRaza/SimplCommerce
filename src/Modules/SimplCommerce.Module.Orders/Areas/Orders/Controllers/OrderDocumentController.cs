using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using SimplCommerce.Infrastructure.Data;
using SimplCommerce.Module.Core.Extensions;
using SimplCommerce.Module.Orders.Models;

namespace SimplCommerce.Module.Orders.Areas.Orders.Controllers
{
    [Area("Orders")]
    [Authorize]
    [ApiExplorerSettings(IgnoreApi = true)]
    public class OrderDocumentController : Controller
    {
        private readonly IRepository<Order> _orderRepository;
        private readonly IWorkContext _workContext;
        private readonly IWebHostEnvironment _hostingEnvironment;

        public OrderDocumentController(IRepository<Order> orderRepository, IWorkContext workContext, IWebHostEnvironment hostingEnvironment)
        {
            _orderRepository = orderRepository;
            _workContext = workContext;
            _hostingEnvironment = hostingEnvironment;
        }

        [HttpGet("user/orders/{orderId}/invoice")]
        public async Task<IActionResult> Download(long orderId, string fileName)
        {
            var user = await _workContext.GetCurrentUser();
            var ownsOrder = _orderRepository.Query().Any(x => x.Id == orderId && x.CustomerId == user.Id);
            if (!ownsOrder)
            {
                return NotFound();
            }

            var path = Path.Combine(_hostingEnvironment.WebRootPath, "user-content", "invoices", fileName);
            if (!System.IO.File.Exists(path))
            {
                return NotFound();
            }

            return PhysicalFile(path, "application/pdf", fileName);
        }
    }
}
