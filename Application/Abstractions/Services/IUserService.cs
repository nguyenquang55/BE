using Application.DTOs.Auth.Response;
using Shared.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Abstractions.Services
{
    public interface IUserService
    {
        Task<Result<LoginResponse>> Refresh(string SsessionToken, CancellationToken ct = default);

    }
}
