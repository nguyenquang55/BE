using Application.Contracts.ThirdParty.Email.Respone;
using Application.Model;
using Shared.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Abstractions.Services
{
    public interface IEmailService
    {
        Task<Result<CheckInboxRespone>> CheckInbox (MberModelRespone modelRespone, Guid userId);
        Task<Result<DeleteEmailRespone>> DeleteEmail(MberModelRespone modelRespone, Guid userId);
        Task<Result<ForwardEmailRespone>> ForwardEmail(MberModelRespone modelRespone, Guid userId);
        Task<Result<ReadEmailRespone>> ReadEmail(MberModelRespone modelRespone, Guid userId);
        Task<Result<ReplyEmailRespone>> ReplyEmail(MberModelRespone modelRespone, Guid userId);
        Task<Result<SearchEmailRespone>> SearchEmail(MberModelRespone modelRespone, Guid userId);
        Task<Result<SendEmailRespone>> SendEmail(MberModelRespone modelRespone, Guid userId);

    }
}
