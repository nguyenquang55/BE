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
        private readonly IEmailService _emailService;
        private readonly ICalendarService _calendarService;

        public LLMService(ICalendarService calendarService,IEmailService emailService)
        {
            _emailService = emailService;
            _calendarService = calendarService;
        }

        public async Task<object> ChooseFuction(MberModelRespone modelRespone, Guid userId)
        {
            object? result = null;
            switch (modelRespone.Intent)
            {
                case "check_inbox":
                    result = await _emailService.CheckInbox(modelRespone, userId);
                    break;
                case "create_event":
                    result= await _calendarService.CreateEvent(modelRespone, userId);
                    break;
                case "delete_email":
                    result = await _emailService.DeleteEmail(modelRespone, userId);
                    break;
                case "delete_event":
                    result  = await _calendarService.DeleteEvent(modelRespone, userId);
                    break;
                case "forward_email":
                    result = await _emailService.ForwardEmail(modelRespone, userId);
                    break;
                case "read_email":
                    result = await _emailService.ReadEmail(modelRespone, userId);
                    break;
                case "reply_email":
                    result = await _emailService.ReplyEmail(modelRespone, userId);
                    break ;
                case "search_email":
                    result = await _emailService.SearchEmail(modelRespone, userId);
                    break;
                case "search_event":
                    result = await _calendarService.SearchEvent(modelRespone, userId);
                    break;
                case "send_email":
                    result = await _emailService.SendEmail(modelRespone, userId);
                    break;
                case "update_event":
                    result = await _calendarService.UpdateEvent(modelRespone, userId);
                    break;
                default:
                    throw new NotSupportedException($"Intent '{modelRespone.Intent}' không được hỗ trợ.");
            }
            return result;
        }
    }
}
