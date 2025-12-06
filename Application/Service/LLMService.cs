using Application.Abstractions.Services;
using Application.Contracts.ThirdParty.Gemini.Respone;
using Application.Model;
using Microsoft.Extensions.Configuration;
using Org.BouncyCastle.Pkcs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Application.Service
{
    public class LLMService : ILLMService
    {
        private readonly ICalendarService _calendarService;

            public LLMService(ICalendarService calendarService)
            {
                _calendarService = calendarService;
            }

        public async Task<object> ChooseFuction(MberModelRespone modelRespone, Guid userId, bool previewOnly = false)
        {
            object? result = null;
            switch (modelRespone.Intent)
            {
                case "create_event":
                    if (previewOnly)
                    {
                        result = await _calendarService.BuildCreatePreviewAsync(modelRespone, userId);
                    }
                    else
                    {
                        result = await _calendarService.CreateEvent(modelRespone, userId);
                    }
                    break;
                case "delete_event":
                    if (previewOnly)
                    {
                        result = await _calendarService.BuildDeletePreviewAsync(modelRespone, userId);
                    }
                    else
                    {
                        result  = await _calendarService.DeleteEvent(modelRespone, userId);
                    }
                    break;
                case "search_event":
                    result = await _calendarService.SearchEvents(modelRespone, userId);
                    break;
                case "update_event":
                    if (previewOnly)
                    {
                        result = await _calendarService.BuildUpdatePreviewAsync(modelRespone, userId);
                    }
                    else
                    {
                        result = await _calendarService.UpdateEvent(modelRespone, userId);
                    }
                    break;
                default:
                    throw new NotSupportedException($"Intent '{modelRespone.Intent}' is not supported.");
            }
            return result;
        }
    }
}
