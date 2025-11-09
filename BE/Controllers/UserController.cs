using Application.Abstractions.Services;
using BE.Controllers.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BE.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : BaseController
    {
        private readonly IUserService _user;
        public UserController(IUserService user)
        {
            _user = user;
        }

        [HttpGet("Refresh")]
        public async Task<IActionResult> Refresh([FromQuery] string SessionToken, CancellationToken ct)
        {
            return await  HandleAsync(_user.Refresh(SessionToken, ct));
        } 
    }
}
