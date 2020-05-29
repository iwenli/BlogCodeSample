using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace webapi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CalcController : ControllerBase
    {
        [Route("[action]")]
        public ActionResult<string> Sum(
            [FromQuery(Name = "num1")] int num1,
            [FromQuery(Name = "num2")] int num2)
        {
            var sum = (num1 + num2).ToString();
            return $"{num1} + {num2} = {sum}.";
        }

        [Route("[action]")]
        public ActionResult<string> SumInts(
            [FromQuery(Name = "num1")] int num1,
            [FromQuery(Name = "num2")] int num2)
        {
            var sum = (num1 + num2).ToString();
            return $"[ints]  {num1} + {num2} = {sum}.";
        }

        [Route("[action]")]
        public ActionResult<string> SumDoubles(
            [FromQuery(Name = "num1")] double num1,
            [FromQuery(Name = "num2")] double num2)
        {
            var sum = (num1 + num2).ToString();
            return $"[doubles]  {num1} + {num2} = {sum}.";
        }
    }
}
