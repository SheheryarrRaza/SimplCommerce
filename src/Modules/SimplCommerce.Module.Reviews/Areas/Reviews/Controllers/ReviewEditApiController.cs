using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SimplCommerce.Module.Core.Extensions;
using SimplCommerce.Module.Reviews.Data;
using SimplCommerce.Module.Reviews.Models;

namespace SimplCommerce.Module.Reviews.Areas.Reviews.Controllers
{
    [Area("Reviews")]
    [Authorize]
    [Route("api/user/reviews")]
    public class ReviewEditApiController : Controller
    {
        private readonly IReviewRepository _reviewRepository;
        private readonly IWorkContext _workContext;

        public ReviewEditApiController(IReviewRepository reviewRepository, IWorkContext workContext)
        {
            _reviewRepository = reviewRepository;
            _workContext = workContext;
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(long id, [FromBody] Review model)
        {
            var user = await _workContext.GetCurrentUser();
            var review = await _reviewRepository.Query().FirstOrDefaultAsync(x => x.Id == id && x.UserId == user.Id);
            if (review == null)
            {
                return NotFound();
            }

            review.Title = model.Title;
            review.Comment = model.Comment;
            review.Rating = model.Rating;
            review.ReviewerName = model.ReviewerName;
            review.Status = model.Status;
            review.EntityId = model.EntityId;
            review.EntityTypeId = model.EntityTypeId;
            review.UserId = model.UserId;
            await _reviewRepository.SaveChangesAsync();

            return NoContent();
        }
    }
}
