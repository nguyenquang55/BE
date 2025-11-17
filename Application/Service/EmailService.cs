using Application.Abstractions.Services;
using Application.Contracts.ThirdParty.Email.Respone;
using Application.Model;
using Shared.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Service
{
    public class EmailService : IEmailService
    {
        public Task<Result<CheckInboxRespone>> CheckInbox(MberModelRespone modelRespone, Guid userId)
        {
            throw new NotImplementedException();
        }

        public Task<Result<DeleteEmailRespone>> DeleteEmail(MberModelRespone modelRespone, Guid userId)
        {
            throw new NotImplementedException();
        }

        public Task<Result<ForwardEmailRespone>> ForwardEmail(MberModelRespone modelRespone, Guid userId)
        {
            throw new NotImplementedException();
        }

        public Task<Result<ReadEmailRespone>> ReadEmail(MberModelRespone modelRespone, Guid userId)
        {
            throw new NotImplementedException();
        }

        public Task<Result<ReplyEmailRespone>> ReplyEmail(MberModelRespone modelRespone, Guid userId)
        {
            throw new NotImplementedException();
        }

        public Task<Result<SearchEmailRespone>> SearchEmail(MberModelRespone modelRespone, Guid userId)
        {
            throw new NotImplementedException();
        }

        public Task<Result<SendEmailRespone>> SendEmail(MberModelRespone modelRespone, Guid userId)
        {
            throw new NotImplementedException();
        }
    }
}
