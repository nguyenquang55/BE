using Application.Abstractions.Services;
using Application.Contracts.ThirdParty.Calendar.Respone;
using Application.Model;
using Shared.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Service
{
    public class CalendarService : ICalendarService
    {
        public Task<Result<CreateEventRespone>> CreateEvent(MberModelRespone modelRespone, Guid userId)
        {
            throw new NotImplementedException();
        }

        public Task<Result<DeleteEventRespone>> DeleteEvent(MberModelRespone modelRespone, Guid userId)
        {
            throw new NotImplementedException();
        }

        public Task<Result<SearchEventRespone>> SearchEvent(MberModelRespone modelRespone, Guid userId)
        {
            throw new NotImplementedException();
        }

        public Task<Result<UpdateEventRespone>> UpdateEvent(MberModelRespone modelRespone, Guid userId)
        {
            throw new NotImplementedException();
        }
    }
}
