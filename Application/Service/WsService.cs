using Application.Abstractions.Services;
using Application.Contracts.SignalR;
using Domain.Entities.Identity;
using Microsoft.AspNetCore.Http;
using Shared.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Service
{
    public class WsService : IWsService
    {
        private readonly ISessionService _sessions;
        public WsService(ISessionService sessions)
        {
            _sessions = sessions;
        }

        public async Task<Result<WsAuthorizeResponse>> ValidateWsRequest(string sessionToken, HttpContext httpContext)
        {
            if (string.IsNullOrWhiteSpace(sessionToken))
            {
                return Result<WsAuthorizeResponse>.FailureResult("Session token is required", statusCode: System.Net.HttpStatusCode.Unauthorized);
            }

            var sessionRes = await _sessions.GetSessionByTokenAsync(sessionToken);
            if (!sessionRes.Success || sessionRes.Data == null)
            {
                return Result<WsAuthorizeResponse>.FailureResult(
                    sessionRes.Message ?? "Unauthorized",
                    statusCode: System.Net.HttpStatusCode.Unauthorized
                );
            }

            var req = httpContext.Request;
            var wsScheme = req.Scheme == "https" ? "wss" : "ws";
            var hubPath = "/hubs/notifications";
            var url = $"{wsScheme}://{req.Host}{hubPath}?sessionToken={Uri.EscapeDataString(sessionToken)}";

            return Result<WsAuthorizeResponse>.SuccessResult(new WsAuthorizeResponse
            {
                Allowed = true,
                Message = "Authorized",
                UserId = sessionRes.Data.UserId
            });
        }
    }
}
