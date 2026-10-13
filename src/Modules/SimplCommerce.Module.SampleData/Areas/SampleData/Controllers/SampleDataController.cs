using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SimplCommerce.Infrastructure;
using SimplCommerce.Module.SampleData.Data;
using SimplCommerce.Module.SampleData.Areas.SampleData.ViewModels;
using SimplCommerce.Module.SampleData.Services;

namespace SimplCommerce.Module.SampleData.Areas.SampleData.Controllers
{
    [Area("SampleData")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public class SampleDataController : Controller
    {
        private readonly ISampleDataService _sampleDataService;
        private readonly ISqlRepository _sqlRepository;

        public SampleDataController(ISampleDataService sampleDataService, ISqlRepository sqlRepository)
        {
            _sampleDataService = sampleDataService;
            _sqlRepository = sqlRepository;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ResetToSample(SampleDataOption model)
        {
            await _sampleDataService.ResetToSampleData(model);
            return Redirect("~/");
        }

        [HttpPost]
        public IActionResult ClearTable(string tableName)
        {
            _sqlRepository.ClearTable(tableName);
            return Redirect("~/");
        }

        [HttpGet]
        public IActionResult Script(string industry, string fileName)
        {
            var path = Path.Combine(GlobalConfiguration.ContentRootPath, "Modules", "SimplCommerce.Module.SampleData", "SampleContent", industry, fileName);
            return PhysicalFile(path, "text/plain", fileName);
        }
    }
}
