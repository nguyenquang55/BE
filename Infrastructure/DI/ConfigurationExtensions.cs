using Microsoft.Extensions.Configuration;
using System.IO;

namespace Infrastructure.DI
{
    public static class ConfigurationExtensions
    {
        public static IConfigurationBuilder AddDefaultConfiguration(this IConfigurationBuilder builder, string sharedFolderRelativePath = "..\\Shared")
        {
            builder.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            var sharedPath = Path.Combine(sharedFolderRelativePath, "appsettings.Shared.json");
            builder.AddJsonFile(sharedPath, optional: false, reloadOnChange: true);
            builder.AddEnvironmentVariables();
            return builder;
        }
    }
}