using Application.Abstractions.Services;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace Application.Service
{
    //public class CurrentUserService : ICurrentUserService
    //{
    //    public Guid? UserId { get; }

    //    public CurrentUserService(IHttpContextAccessor accessor)
    //    {
    //        var userIdStr = accessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
    //        if (Guid.TryParse(userIdStr, out var id))
    //        {
    //            UserId = id;
    //        }
    //    }
    //}
}
