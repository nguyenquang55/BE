using Application.Contracts.SignalR;
using Microsoft.AspNetCore.Http;
using Shared.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Abstractions.Services
{
    public interface IWsService
    {
        Task <Result<WsAuthorizeResponse>> ValidateWsRequest(string sessionToken, HttpContext httpContext); 
    }
}
