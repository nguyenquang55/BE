using Application.Abstractions.Repositories.Common;
using Domain.Entities.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Abstractions.Repositories
{
    public interface IOAuthTokenRepository : IRepository<OAuthToken>
    {
    }
}
