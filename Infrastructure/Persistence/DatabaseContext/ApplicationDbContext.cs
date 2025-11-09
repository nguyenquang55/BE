using Application.Abstractions.Infrastructure;
using Domain.Common;
using Domain.Entities;
using Domain.Entities.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;

namespace Infrastructure.Persistence.DatabaseContext
{
    public class ApplicationDbContext : IdentityDbContext<IdentityUser>, IApplicationDbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }
        public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
        public new DbSet<User> Users => Set<User>();
        public DbSet<OAuthProvider> OAuthProviders => Set<OAuthProvider>();
        public DbSet<OAuthToken> OAuthTokens => Set<OAuthToken>();
        public DbSet<Email> Emails => Set<Email>();
        public DbSet<EmailThread> EmailThreads => Set<EmailThread>();   
        public DbSet<Contact> Contacts => Set<Contact>();
        public DbSet<Session> Sessions => Set<Session>();
        public DbSet<Calendar> Calendars => Set<Calendar>();
        public DbSet<Request> Requests => Set<Request>();
        public DbSet<UserSchedule> UserSchedules => Set<UserSchedule>();



        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder
                .EnableSensitiveDataLogging()
                .LogTo(Console.WriteLine, LogLevel.Information);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
        }
    }
}
