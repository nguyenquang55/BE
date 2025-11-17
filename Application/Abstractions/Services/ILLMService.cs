using Application.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Abstractions.Services
{
    public interface ILLMService
    {
        Task<object> ChooseFuction(MberModelRespone modelRespone,Guid userId);
    }
}
