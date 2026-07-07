using Infrastructure.DI;
using BE.DI;

var builder = WebApplication.CreateBuilder(args);

// 1. Tải cấu hình mặc định (Shared + Web)
builder.Configuration.AddDefaultConfiguration();

// 2. Đăng ký các dịch vụ Dependency Injection (DI)
builder.Services
    .AddApplicationServices()
    .AddInfrastructure(builder.Configuration)
    .AddWebServices(builder.Configuration)
    .AddWebApiMessaging(builder.Configuration);

var app = builder.Build();

// 3. Cấu hình Middleware Pipeline & Routing
app.ConfigureMiddlewarePipeline();

app.Run();
