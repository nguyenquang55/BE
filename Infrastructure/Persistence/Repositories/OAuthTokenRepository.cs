using Application.Abstractions.Repositories;
using Domain.Entities.Identity;
using Infrastructure.Persistence.DatabaseContext;
using Infrastructure.Persistence.Repositories.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Persistence.Repositories
{
    public class OAuthTokenRepository : Repository<OAuthToken>, IOAuthTokenRepository
    {
        public OAuthTokenRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}
