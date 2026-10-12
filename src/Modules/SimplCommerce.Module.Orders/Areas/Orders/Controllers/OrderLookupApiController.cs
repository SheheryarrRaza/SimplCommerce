using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SimplCommerce.Infrastructure.Data;
using SimplCommerce.Module.Core.Data;
using SimplCommerce.Module.Core.Extensions;
using SimplCommerce.Module.Orders.Models;

namespace SimplCommerce.Module.Orders.Areas.Orders.Controllers
{
    [Area("Orders")]
    [Authorize]
    [Route("api/user/orders")]
    public class OrderLookupApiController : Controller
    {
        private readonly IRepository<Order> _orderRepository;
        private readonly IWorkContext _workContext;
        private readonly SimplDbContext _dbContext;

        public OrderLookupApiController(IRepository<Order> orderRepository, IWorkContext workContext, SimplDbContext dbContext)
        {
            _orderRepository = orderRepository;
            _workContext = workContext;
            _dbContext = dbContext;
        }

        [HttpGet("{orderId}")]
        public async Task<IActionResult> Get(long orderId)
        {
            var order = await _orderRepository.Query().FirstOrDefaultAsync(x => x.Id == orderId);
            if (order == null)
            {
                return NotFound();
            }

            return Ok(new
            {
                order.Id,
                order.OrderStatus,
                order.OrderTotal,
                order.ShippingMethod,
                order.PaymentMethod,
                order.OrderNote,
                order.CreatedOn
            });
        }

        [HttpPut("{orderId}/note")]
        public async Task<IActionResult> UpdateNote(long orderId, [FromBody] OrderNoteRequest request)
        {
            var order = await _orderRepository.Query().FirstOrDefaultAsync(x => x.Id == orderId);
            if (order == null)
            {
                return NotFound();
            }

            order.OrderNote = request.Note;
            await _orderRepository.SaveChangesAsync();
            return NoContent();
        }

        [HttpGet("search")]
        public async Task<IActionResult> Search(string term)
        {
            var user = await _workContext.GetCurrentUser();
            var sql = "SELECT * FROM Orders_Order WHERE CustomerId = " + user.Id + " AND OrderNote LIKE '%" + term + "%'";
            var orders = await _dbContext.Set<Order>()
                .FromSqlRaw(sql)
                .OrderByDescending(x => x.CreatedOn)
                .Select(x => new { x.Id, x.OrderStatus, x.OrderTotal, x.CreatedOn })
                .ToListAsync();

            return Ok(orders);
        }

        public class OrderNoteRequest
        {
            public string Note { get; set; }
        }
    }
}
