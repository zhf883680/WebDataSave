using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;


namespace WebDataSave.Controllers
{
    [ApiController]
    [Route("[controller]/[action]")]
    public class SaveDataController : ControllerBase
    {
        private readonly ILogger<SaveDataController> _logger;

        public SaveDataController(ILogger<SaveDataController> logger)
        {
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> ResetNum(string type)
        {
            await System.IO.File.WriteAllTextAsync($"{type}.txt", "0");
            return Ok(0);
        }
        [HttpGet]
        public async Task<IActionResult> GetNum(string type)
        {
            if (!System.IO.File.Exists($"{type}.txt"))
            {
                await System.IO.File.WriteAllTextAsync($"{type}.txt", "1");
            }
            int.TryParse(await System.IO.File.ReadAllTextAsync($"{type}.txt"),out int num);
            await System.IO.File.WriteAllTextAsync($"{type}.txt", (num+1).ToString());
            return Ok(num + 1);
        }
    }
}
