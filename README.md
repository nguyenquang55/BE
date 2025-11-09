# BE

## SignalR Notifications

Khi một client kết nối tới hub `/hubs/notifications`, server sẽ tự động gửi một thông điệp chào dạng:

```
Hello <ConnectionId>
```

Message này được gửi qua method SignalR tên `notification` (thống nhất với các gửi khác). Ở phía client bạn cần đăng ký:

```js
connection.on('notification', msg => {
	console.log('Notification:', msg);
});
```

Sau đó khởi tạo kết nối như bình thường. Ngay khi `start()` thành công bạn sẽ thấy log Hello.

Nếu muốn phân biệt loại notification, có thể chuẩn hoá payload dạng JSON:

```json
{
	"type": "greeting",
	"message": "Hello <ConnectionId>"
}
```

Hiện tại server gửi plain text. Bạn có thể sửa ở `NotificationHub.OnConnectedAsync` để gửi JSON nếu cần.

### Xử lý message hai pha (ack + processed)

Client gọi method hub:
```js
connection.invoke('ProcessMessage', 'Noi dung can xu ly', 'msg-123');
```

Server sẽ gửi 2 gói `notification`:
1. Ack:
```json
{
	"type": "ack",
	"messageId": "msg-123",
	"receivedAt": "2025-11-09T00:00:00Z",
	"connectionId": "<id>"
}
```
2. Processed:
```json
{
	"type": "processed",
	"messageId": "msg-123",
	"payload": "{\"type\":\"processed\",...}",
	"connectionId": "<id>"
}
```

Client nên phân nhánh theo `type`:
```js
connection.on('notification', data => {
	switch (data.type) {
		case 'ack':
			// hiển thị trạng thái đang xử lý
			break;
		case 'processed':
			// cập nhật kết quả cuối cùng
			const payload = JSON.parse(data.payload);
			console.log('Kết quả xử lý:', payload);
			break;
		default:
			console.log('Notification:', data);
	}
});
```