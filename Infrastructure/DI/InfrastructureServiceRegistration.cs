using Application.Abstractions.Common;
using Application.Abstractions.Infrastructure;
using Application.Abstractions.Repositories;
using Ecom.Infrastructure.Persistence;
using Infrastructure.Cache;
using Infrastructure.Persistence.DatabaseContext;
using Infrastructure.Persistence.JWT;
using Infrastructure.Persistence.Repositories;
using Infrastructure.Persistence.Repositories.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nest;
using StackExchange.Redis;
using System;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Infrastructure.DI
{
    public static class InfrastructureServiceRegistration
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config, bool addOutboxPublisher = true)
        {
            services.AddDatabase(config)
                    .AddRepositories()
                    .AddCaching(config)
                    .AddJwtAuthentication(config);

            // SignalR services are registered in the Web (BE) project to avoid cross-project hub references

            //// MassTransit publisher (WebApi) registration
            //MassTransitConfig.AddMassTransitPublisher(services, config);

            //// Register Outbox publisher hosted service optionally
            //if (addOutboxPublisher) services.AddHostedService<OutboxPublisherService>();

            return services;
        }

        public static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration config)
        {
            services.AddDbContext<ApplicationDbContext>(opts => opts.UseSqlServer(config.GetConnectionString("DefaultConnection")));
            return services;
        }

        public static IServiceCollection AddRepositories(this IServiceCollection services)
        {
            services.AddScoped(typeof(Application.Abstractions.Repositories.Common.IRepository<>), typeof(Repository<>));
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            return services;
        }

        public static IServiceCollection AddCaching(this IServiceCollection services, IConfiguration config)
        {
            services.AddSingleton<IConnectionMultiplexer>(sp => ConnectionMultiplexer.Connect(config["Redis:Port"] ?? "localhost:6379"));
            services.AddSingleton<IRedisCacheService, RedisCacheService>();
            return services;
        }

        //public static IServiceCollection AddSearch(this IServiceCollection services, IConfiguration config)
        //{
        //    var settings = new ConnectionSettings(new Uri(config["Elastic:Url"] ?? "http://elasticsearch:9200")).DefaultIndex("products");
        //    services.AddSingleton<IElasticClient>(new ElasticClient(settings));
        //    services.AddSingleton<ElasticService>();
        //    return services;
        //}

        public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration config)
        {
            var jwtSettings = config.GetSection("Jwt");
            var key = Encoding.UTF8.GetBytes(jwtSettings["Key"] ?? throw new Exception("JWT key missing"));

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ClockSkew = TimeSpan.Zero
                };
            });

            services.AddScoped<IJwtProvider, JwtProvider>();

            return services;
        }
    }
}