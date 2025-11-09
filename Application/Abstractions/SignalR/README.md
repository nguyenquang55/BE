/*
SIGNALR ARCHITECTURE EXPLANATION
================================

Tại sao có 2 interfaces và cách xử lý bất đồng bộ:

1. INotificationHub (Application.Abstractions.SignalR)
   - Interface cho Application Layer sử dụng
   - Được implement bởi NotificationHubAdapter (Infrastructure)
   - Chịu trách nhiệm: Cung cấp API cho business logic gọi

2. INotificationHubContext (Application.Abstractions.SignalR)
   - Interface cho SignalR Hub concrete class
   - Được implement bởi NotificationHub (Presentation)
   - Chịu trách nhiệm: Định nghĩa contract cho SignalR operations thực tế

3. NotificationHubAdapter (Infrastructure.SignalR)
   - Adapter pattern: Kết nối Application với Infrastructure
   - Inject INotificationHubContext để gọi SignalR thực tế
   - Implement INotificationHub để Application sử dụng

4. NotificationService (Application.Service)
   - Business logic layer
   - Sử dụng INotificationHub để gửi notification
   - Xử lý validation, logging, error handling

XỬ LÝ BẤT ĐỒNG BỘ (Async/Await):
- Tất cả methods đều là async để không block thread
- SignalR I/O operations là asynchronous by nature
- Async pattern cho phép handle high-throughput scenarios
- Non-blocking I/O cải thiện performance và scalability

LUỒNG HOẠT ĐỘNG:
1. Controller/Service gọi NotificationService.SendToUserAsync()
2. NotificationService gọi _hub.SendToUser() (INotificationHub)
3. NotificationHubAdapter gọi _hubContext.SendToUserAsync() (INotificationHubContext)
4. NotificationHub thực hiện SignalR operation thực tế

LỢI ÍCH:
- Clean Architecture: Tách biệt concerns rõ ràng
- Testability: Có thể mock từng layer
- Scalability: Async pattern hỗ trợ high-throughput
- Maintainability: Code dễ hiểu và mở rộng
*/
