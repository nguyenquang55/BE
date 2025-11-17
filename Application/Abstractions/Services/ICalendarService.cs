using Application.Contracts.ThirdParty.Calendar.Respone;
using Application.Model;
using Shared.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Abstractions.Services
{
    public interface ICalendarService
    {
        Task<Result<CreateEventRespone>> CreateEvent(MberModelRespone modelRespone, Guid userId);
        Task<Result<DeleteEventRespone>> DeleteEvent(MberModelRespone modelRespone, Guid userId);
        Task<Result<UpdateEventRespone>> UpdateEvent(MberModelRespone modelRespone, Guid userId);
        Task<Result<SearchEventRespone>> SearchEvent(MberModelRespone modelRespone, Guid userId);
    }
}
