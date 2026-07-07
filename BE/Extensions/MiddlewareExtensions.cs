using Application.Abstractions.Services;
using BE.Hubs;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BE.Extensions
{
    public static class MiddlewareExtensions
    {
        public static WebApplication ConfigureMiddlewarePipeline(this WebApplication app)
        {
            // 1. Swagger UI in development environment
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            // 2. Security & Routing Middleware
            app.UseHttpsRedirection();

            // Call UseCors BEFORE authentication/authorization
            app.UseCors("AllowAll");

            app.UseAuthentication();
            app.UseAuthorization();

            // 3. Routing mappings
            app.MapControllers();
            app.MapHub<NotificationHub>("/hubs/notifications");

            // 4. Heavy singletons warm-up at startup
            _ = app.Services.GetService<ITokenizerService>();

            return app;
        }
    }
}
