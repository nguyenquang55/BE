# CalendarBot Backend

Backend cho tro ly lich thong minh, cho phep nguoi dung giao tiep real-time de tao, cap nhat, tim kiem, xoa su kien tren Google Calendar thong qua NLP/LLM + ONNX intent classification.

Du an su dung mo hinh monorepo .NET voi 2 runtime chinh:
- BE: Web API + SignalR Hub + Outbox Publisher
- Worker: Message consumer xu ly AI/Calendar bat dong bo

## 1) Tinh nang chinh

- Xac thuc nguoi dung: register, login, logout, session token.
- OTP qua email de xac minh.
- Lien ket Google OAuth va dong bo quyen Calendar.
- Real-time messaging qua SignalR.
- Xu ly bat dong bo qua RabbitMQ + MassTransit.
- Phan loai intent bang ONNX model.
- Goi Gemini de parse/composition noi dung tu nhien.
- Outbox pattern de publish integration event an toan hon.

## 2) Kien truc tong quan

Request flow rut gon:

1. Client goi API de xac thuc va lay session token.
2. Client ket noi SignalR hub voi session token.
3. Tin nhan nguoi dung duoc enqueue thanh event user.msg.submitted.queue.
4. Worker tieu thu event, classify intent (ONNX), goi Calendar/Gemini, sau do publish preview hoac ket qua processed.
5. BE consumers nhan event processed/preview va day ve client qua SignalR.
6. OutboxPublisherService trong BE poll bang OutboxMessages va publish cac event con ton.

## 3) Cong nghe chinh

- .NET 8
- ASP.NET Core Web API
- SignalR + MessagePack
- Entity Framework Core + SQL Server
- Redis
- RabbitMQ + MassTransit
- Google Calendar API
- Google OAuth2
- Gemini API
- ONNX Runtime + Microsoft.ML.Tokenizers

## 4) Cau truc solution

- BE/: API host, controllers, hub, consumers day real-time
- Worker/: consumer xu ly message bat dong bo
- Application/: business service, DTO, abstraction
- Infrastructure/: repository, dbcontext, redis, JWT, outbox, background services
- Domain/: entities, enums, base classes
- Shared/: contracts dung chung giua BE va Worker

## 5) Yeu cau he thong

- .NET SDK 8.x
- SQL Server
- Redis (mac dinh localhost:6379)
- RabbitMQ (mac dinh localhost, user/pass guest/guest)

Khuyen nghi:
- Visual Studio 2022 hoac VS Code + C# extension
- Postman hoac Bruno de test API

## 6) Cau hinh moi truong

File cau hinh hien tai:
- BE/appsettings.json
- Worker/appsettings.json

Canh bao bao mat:
- Khong de credentials that trong appsettings len repository cong khai.
- Nen chuyen secrets sang User Secrets, environment variables, hoac secret manager.
- Neu da lo thong tin, hay rotate ngay: OAuth client secret, SMTP app password, JWT key, DB password.

Gia tri can review truoc khi chay:
- ConnectionStrings:DefaultConnection
- Redis:Port
- RabbitMq:Host, Username, Password, VirtualHost
- OAuth:Google:ClientId, ClientSecret, RedirectUri
- GEMINI_API_Key
- Jwt:Key
- Session:ExpireTimeMinutes
- Model:OnnxPath, VocabPath, InputIdsName, TokenTypeIdsName, AttentionMaskName, OutputName

## 7) Chay du an local

Mo terminal tai thu muc goc solution.

### 7.1 Restore dependencies

```bash
dotnet restore
```

### 7.2 Khoi tao database

Du an dung ApplicationDbContext o Infrastructure.

Neu da co migrations, chay:

```bash
dotnet ef database update --project Infrastructure --startup-project BE
```

Neu chua co migrations trong repo, tao migration dau tien:

```bash
dotnet ef migrations add InitialCreate --project Infrastructure --startup-project BE
dotnet ef database update --project Infrastructure --startup-project BE
```

### 7.3 Chay API

```bash
dotnet run --project BE
```

Mac dinh profile development:
- HTTP: http://localhost:5246
- HTTPS: https://localhost:7127
- Swagger: /swagger

### 7.4 Chay Worker

Mo terminal thu hai:

```bash
dotnet run --project Worker
```

Mac dinh profile development:
- HTTP: http://localhost:5132
- HTTPS: https://localhost:7261

## 8) API va endpoint quan trong

Auth:
- POST /api/Auth/register
- POST /api/Auth/login
- POST /api/Auth/refresh
- POST /api/Auth/logout
- POST /api/Auth/SendOTP
- POST /api/Auth/VerifyOTP

OAuth:
- GET /api/OAuth
- GET /api/OAuth/callback
- GET /api/OAuth/Refresh

User:
- GET /api/User/Refresh

Contacts:
- GET /api/Contacts
- GET /api/Contacts/{id}
- POST /api/Contacts
- PUT /api/Contacts/{id}
- DELETE /api/Contacts/{id}

WebSocket bootstrap:
- POST /api/ws

SignalR hub:
- /hubs/notifications

## 9) RabbitMQ queues dang dung

BE consume:
- user.msg.processed.queue
- user.msg.preview.queue

Worker consume:
- user.msg.submitted.queue
- user.msg.confirmed.queue

## 10) Background services

Trong BE:
- RedisHealthCheckBgrService
- CallendarEvntNotificationBgrService
- CalendarCacheRefreshBgrService
- OutboxPublisherService

## 11) Van de da biet

- Mot so method trong SessionService chua implement hoan toan.
- Mo hinh ONNX hien dang co xu huong dung duong dan tuyet doi neu khong cau hinh dung, can dieu chinh de deploy linh hoat.
- README nay mo ta setup local. Khi deploy production can bo sung:
	- policy CORS va authentication day du
	- observability (structured logs, tracing, metrics)
	- rotate va quan ly secret tap trung

## 12) Kiem tra nhanh sau khi chay

1. Mo Swagger cua BE va login de lay session token.
2. Goi /api/ws de lay URL websocket authorize.
3. Ket noi SignalR den /hubs/notifications voi sessionToken.
4. Gui message test, theo doi preview/processed duoc day ve realtime.
5. Kiem tra Worker log va RabbitMQ management UI de xac nhan luong queue thong suot.

## 13) Dinh huong cai tien

- Hoan thien refresh/revoke session lifecycle.
- Tach appsettings theo moi truong (Development/Staging/Production).
- Them integration tests cho luong Auth/OAuth/SignalR/Queue.
- Chuan hoa API error contract va bo sung idempotency cho message processing.


