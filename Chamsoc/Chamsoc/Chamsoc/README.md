# Chamsoc Project

## Yêu cầu hệ thống
- .NET SDK 8.0.0 hoặc cao hơn
- Visual Studio 2022 hoặc Visual Studio Code
- SQL Server

## Các bước cài đặt

1. Clone repository về máy local

2. Mở terminal và chạy các lệnh sau:
```bash
dotnet restore
dotnet build
```

3. Cấu hình connection string trong `appsettings.json`

4. Chạy migration để tạo database:
```bash
dotnet ef database update
```

5. Chạy ứng dụng:
```bash
dotnet run
```

## Xử lý lỗi thường gặp

### Lỗi package không tìm thấy
Nếu gặp lỗi về package, hãy thử các bước sau:
1. Xóa thư mục bin và obj
2. Chạy lệnh `dotnet restore --force`
3. Build lại project

### Lỗi Entity Framework
Nếu gặp lỗi về Entity Framework:
1. Đảm bảo đã cài đặt .NET SDK 8.0.0
2. Chạy lệnh `dotnet tool install --global dotnet-ef`
3. Chạy lại migration

## Liên hệ hỗ trợ
Nếu gặp vấn đề, vui lòng liên hệ admin để được hỗ trợ. 