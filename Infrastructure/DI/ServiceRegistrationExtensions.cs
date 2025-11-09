using Application.Validators;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using Microsoft.AspNetCore.Http;
using System.Reflection;

namespace Infrastructure.DI
{
    public static class ServiceRegistrationExtensions
    {
        /// <summary>
        /// Đăng ký tự động Service và Repository dựa theo convention.
        /// </summary>
        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            // Đăng ký MediatR
            //services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Application.EventHandlers.ProductCreatedDomainEventHandler).Assembly));

            // ...existing code...
            const string serviceInterfaceNamespace = "Application.Abstractions.Services";
            const string repositoryInterfaceNamespace = "Application.Abstractions.Repositories";

            // Định nghĩa các namespace chứa implementation
            const string serviceImplNamespace = "Application.Service";
            const string repositoryImplNamespace = "Infrastructure.Persistence.Repositories";

            //Các namespace BackgroundService
            const string backgroundServiceNamespace = "Infrastructure.BackgroundServices";

            // Lấy tất cả các assembly trong AppDomain
            var allAssemblies = AppDomain.CurrentDomain.GetAssemblies();

            // Tìm các interface service
            var serviceInterfaces = allAssemblies
                .SelectMany(a => a.GetTypes())
                .Where(t => t.IsInterface && t.Namespace == serviceInterfaceNamespace && t.Name.EndsWith("Service"))
                .ToList();

            // Tìm các interface repository
            var repositoryInterfaces = allAssemblies
                .SelectMany(a => a.GetTypes())
                .Where(t => t.IsInterface && t.Namespace == repositoryInterfaceNamespace && t.Name.EndsWith("Repository"))
                .ToList();

            // Tìm các BackgroundService
            var BackgroundServiceImplementations = allAssemblies
                 .SelectMany(a => a.GetTypes())
                 .Where(t => t.IsClass && !t.IsAbstract && t.Namespace == backgroundServiceNamespace)
                 .ToList();

            // Tìm tất cả class implementation từ Application.Service
            var serviceImplementations = allAssemblies
                .SelectMany(a => a.GetTypes())
                .Where(t => t.IsClass && !t.IsAbstract && t.Namespace == serviceImplNamespace)
                .ToList();

            // Tìm tất cả class implementation từ Infrastructure.Persistence.Repositories
            var repositoryImplementations = allAssemblies
                .SelectMany(a => a.GetTypes())
                .Where(t => t.IsClass && !t.IsAbstract && t.Namespace == repositoryImplNamespace)
                .ToList();

            // Đăng ký Service
            foreach (var iface in serviceInterfaces)
            {
                var impl = serviceImplementations.FirstOrDefault(c => $"I{c.Name}" == iface.Name);
                if (impl != null)
                    services.AddScoped(iface, impl);
            }

            // Đăng ký Repository
            foreach (var iface in repositoryInterfaces)
            {
                var impl = repositoryImplementations.FirstOrDefault(c => $"I{c.Name}" == iface.Name);
                if (impl != null)
                    services.AddScoped(iface, impl);
            }

            //// Đăng ký BackgroundService
            //foreach (var impl in BackgroundServiceImplementations)
            //{
            //    services.Add(new ServiceDescriptor(typeof(Microsoft.Extensions.Hosting.IHostedService), impl, ServiceLifetime.Singleton));
            //}

            // Đăng ký các validator từ Application.Validators
            var validatorTypes = typeof(LoginRequestValidator).Assembly
                    .GetTypes()
                    .Where(t => t.IsClass && !t.IsAbstract && t.Namespace == "Application.Validators" && t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IValidator<>)))
                    .ToList();

            foreach (var validatorType in validatorTypes)
            {
                var validatorInterface = validatorType.GetInterfaces()
                    .First(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IValidator<>));
                services.AddScoped(validatorInterface, validatorType);
                Console.WriteLine($"Đã đăng ký validator: {validatorType.FullName} cho {validatorInterface.FullName}");
            }

            return services;
        }
    }
}