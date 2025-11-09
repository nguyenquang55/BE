using Microsoft.AspNetCore.Mvc;
using Shared.Common;

namespace BE.Controllers.Common
{
    public abstract class BaseController : ControllerBase
    {
        protected async Task<IActionResult> HandleAsync<T>(Task<Result<T>> task)
        {
            var res = await task;
            return StatusCode((int)res.StatusCode, res);
        }
    }
}
