using Application.Abstractions.Repositories;
using Domain.Entities.Identity;
using Infrastructure.Persistence.DatabaseContext;
using Infrastructure.Persistence.Repositories.Common;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

namespace Infrastructure.Persistence.Repositories
{
    public class AuthRepository : Repository<User>, IAuthRepository
    {
        private readonly ApplicationDbContext _context;

        public AuthRepository(ApplicationDbContext context) : base(context)
        {
            _context = context;  
        }

        /// <summary>
        /// Lấy user theo email (không phân biệt hoa thường)
        /// </summary>
        /// <param name="email">Email người dùng</param>
        /// <returns>User hoặc null nếu không tồn tại</returns>
        public async Task<User?> GetUserByEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                throw new ArgumentException("Email không được để trống", nameof(email));

            var normalizedEmail = email.Trim().ToLowerInvariant();

            return await _context.Users.Where(u => u.Email!.ToLower() == normalizedEmail).FirstOrDefaultAsync();   
        }
    }
}
