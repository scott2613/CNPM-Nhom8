# AudioGuide — Thuyết minh tự động đa ngôn ngữ

Trường Đại học Sài Gòn · Nhóm 8 · DCT124C4

Một QR cổng mở bản đồ. Du khách chọn POI và ngôn ngữ hoặc bật GPS sau khi chọn tầng. Admin quản lý POI, thiết bị và thống kê. Giao diện Liquid Glass được giữ trong các trang gốc.

## Chạy trên Windows và Visual Studio

1. Chuẩn bị SQL Server. Với CSDL `AudioGuideDb` đã có dữ liệu, chọn đúng CSDL trong SSMS rồi mở và chạy `sql/005_AdminAccounts.sql`. Script chỉ thêm bảng tài khoản nếu bảng chưa có. Với CSDL mới, chạy `Database/AudioGuideDB.sql` để tạo bảng và dữ liệu mẫu.
2. SQL Server mặc định là `localhost\MSSQLSERVER03`. Nếu máy dùng tên khác, sửa chuỗi kết nối trong `AudioGuide.API/appsettings.Development.json`.
3. Mở `AudioGuideSolution.sln`, chọn **AudioGuide Local (API + Admin)** và bấm **F5**. Nếu Visual Studio không hiện profile chung, mở **Configure Startup Projects → Multiple startup projects**, đặt `AudioGuide.API` và `AudioGuide.Admin` thành **Start** và chọn profile `https` cho Admin.
4. Mở **https://localhost:7288**. Nếu CSDL chưa có tài khoản, hệ thống nạp mã băm từ `Security:AdminPasswordHash` hoặc file mật khẩu Development cũ nếu có. Nếu chưa từng đặt, trang localhost sẽ cho tạo mật khẩu tối thiểu 4 ký tự. Sau đó đăng nhập. API Hub ở **https://localhost:7084/AudioGuideHub.html**.
5. Vào **Tài khoản**, nhập mật khẩu hiện tại và điền tên đăng nhập mới, mật khẩu mới hoặc cả hai. Để trống mục muốn giữ. Lưu xong, đăng nhập lại với thông tin mới.

Tài khoản được lưu trong `dbo.AdminAccounts` của CSDL đang dùng. Các máy mở cùng trang Admin hoặc dùng chung API và CSDL thấy cùng thông tin tài khoản. Máy dùng CSDL riêng quản lý tài khoản riêng. Nếu chạy Admin riêng trên máy khác và dùng chung API, đặt `ApiSettings:BaseUrl` đến API đó và cấu hình cùng `Security:AdminApiKey` ở hai phía. Giữ khóa này ở cấu hình riêng của máy chủ. Khi chạy Admin và API trên cùng máy với profile Development, khóa API nội bộ vẫn được tạo tự động.

Cấu hình tên đăng nhập và mã băm từ JSON hoặc môi trường được dùng để khởi tạo khi bảng chưa có tài khoản. Thay đổi qua trang **Tài khoản** được ghi vào CSDL. Sau khi đổi thông tin, các phiên đăng nhập cũ sẽ bị yêu cầu đăng nhập lại ở lần truy cập tiếp theo. Bản sao lưu SQL được tạo sau lần đổi sẽ chứa cả bản ghi tài khoản.

Nếu mở Admin qua Visual Studio Dev Tunnel, thêm tên miền chính xác của tunnel Admin vào `AllowedHosts` trong `AudioGuide.Admin/appsettings.Development.json`, rồi khởi động lại Admin. Ghi tên miền riêng, bỏ phần giao thức, cổng và đường dẫn. Bản này đã khai báo `nq88b866-7288.asse.devtunnels.ms`. Tạo mật khẩu lần đầu tại `https://localhost:7288/Account/Login` rồi dùng cùng mật khẩu để đăng nhập qua tunnel. Khi API và Admin chạy cùng máy, `ApiSettings:BaseUrl` của Admin có thể dùng `https://localhost:7084`.

Để quét QR bằng điện thoại, đặt `PublicBaseUrl` thành địa chỉ HTTPS mà điện thoại truy cập được và cập nhật `AllowedHosts`. Cấu hình Development đang dùng tunnel API `nq88b866-7084.asse.devtunnels.ms`. Nếu Visual Studio cấp tên miền mới, sửa `PublicBaseUrl` và `AllowedHosts` trong `AudioGuide.API/appsettings.Development.json`, chạy lại API rồi tạo QR mới. Mở API Console qua tunnel để tải QR dành cho khách, hoặc in QR từ `/api/QrGuide/generate/MUSEUM-MAIN-ENTRY`. QR cho khách do Console tạo luôn mở với `preview=false`; tùy chọn xem thử chỉ áp dụng cho yêu cầu thực thi trên Console.

## Hai file SQL chính trong SSMS

1. **Database/AudioGuideDB.sql** chứa đầy đủ cấu trúc, nâng cấp và bộ mẫu 8 điểm × 5 ngôn ngữ. Database mới chỉ cần mở file và Execute. Với database cũ, sao lưu rồi đặt `@UpgradeLegacy = 1` ở đầu file. Nếu muốn thay bài thuyết minh ở 8 mã mẫu đang có, đặt thêm `@RefreshDemoNarratives = 1`. Mặc định giữ bài đã có.
2. **Database/AudioGuideDB_DisplayTable.sql** hiển thị bốn bảng kết quả chỉ đọc gồm địa điểm, nội dung đa ngôn ngữ, thiết bị và nhật ký sử dụng. Đổi `@Page` và `@PageSize` để xem từng trang. SQL lưu mốc truy cập, còn trạng thái hiện diện lấy từ heartbeat của API.

Dashboard phân trang 10/25/50 dòng cho điểm tham quan, thiết bị, phiên trực tiếp và lịch sử. Bộ lọc tìm trên tập dữ liệu trang quản trị đã tải. Bảng thiết bị lấy tối đa 500 bản ghi gần nhất theo hợp đồng API hiện có. Nhãn online cập nhật mỗi 5 giây và hết hạn theo `Presence:TtlSeconds` (mặc định 20 giây).

Trong **Dashboard & Quản lý POI**, biểu đồ **Lượt khách vào cổng theo giờ** đếm sự kiện QR cổng `GateEntry` theo 24 khung giờ Việt Nam. Bộ lọc gồm hôm nay, 7 ngày hoặc 30 ngày tính cả hôm nay. Các khung giờ cùng giờ bắt đầu được cộng dồn trong kỳ.

Hai biểu đồ dùng bộ chọn thời gian xổ ngang. Trỏ, chạm hoặc dùng phím Tab đến một cột để xem số lượt và phần trăm thay đổi. Kỳ đối chiếu lùi 1, 7 hoặc 30 ngày, giữ cùng độ dài và giờ chốt. Lượt tăng từ 0 được hiển thị là **Có lượt mới**.

Từ Dashboard, chọn **Thống kê địa điểm** để cuộn tới biểu đồ so sánh lượt mở điểm (`PoiViewed`) và lượt bắt đầu nghe (`PlayStarted`). Có thể lọc theo địa điểm và 1, 7 hoặc 30 ngày tính cả hôm nay. Lượt mở được ghi khi khách chọn điểm trên bản đồ hoặc GPS mở điểm, bắt đầu từ phiên bản này. Sự kiện mới dùng bảng nhật ký hiện có.

## Tích hợp và dữ liệu

- Bật Cloud Translation API cho dự án Google Cloud, rồi đặt khóa tại **AudioGuide.API**. Trong Visual Studio chọn **Manage User Secrets** và thêm cấu hình `"Translation": { "ApiKey": "YOUR_KEY" }` vào đối tượng JSON hiện có. Khi triển khai, dùng biến môi trường `Translation__ApiKey`. Khởi động lại API sau khi cấu hình. Không đưa khóa vào Git.
- Trong Admin, thêm POI và nhập tiêu đề, bài thuyết minh bằng tiếng Việt. Lần lưu tự dịch tiêu đề và bài sang Anh, Trung giản thể, Pháp và Nga. Nếu thiếu khóa hoặc dịch lỗi, lần lưu không ghi dữ liệu. Nội dung đã lưu trước đó vẫn giữ nguyên. Với điểm cũ thiếu bản dịch, mở chỉnh sửa rồi bấm lưu để bổ sung.
- Nội dung mẫu gồm 8 địa điểm × 5 ngôn ngữ. Với DB đã có 8 địa điểm mã 1–8, sao lưu, nâng cấp schema nếu cần, sau đó chọn **AudioGuideDb** trong SSMS và chạy `sql/004_UpdateDemoNarratives.sql` để áp dụng 40 bài. Script thay tiêu đề và bài thuyết minh cho các mã mẫu, giữ các POI và lịch sử. Các tham chiếu audio của bài bị thay đổi được xóa để trình phát không đọc tệp cũ. Nếu bạn đã tự biên tập nội dung cho 8 mã mẫu, hãy đọc và đối chiếu script trước khi chạy.
- API cổng dùng cookie định danh trình duyệt. Một trình duyệt có thể đổi định danh khi xóa cookie. Khi mở bản đồ khách, trình duyệt gửi tín hiệu online ngay và lặp mỗi 3 giây khi tab hiển thị. **Giám sát Real-time** đọc các phiên này từ RAM. Phiên hết hạn sau 20 giây mặc định và mất khi API khởi động lại.
- Luồng chính dùng Web Speech. Nguồn đọc dự phòng phụ thuộc mạng, trình duyệt và nhà cung cấp. Tính năng GPS cần HTTPS và quyền vị trí. Demo trong nhà nên giữ phương án chọn POI thủ công.
- Log cũ được giữ dưới nhóm Legacy. Thống kê mới chỉ đọc dữ liệu có IsUtc. Không tự chuyển giờ các bản ghi chưa xác minh.
- `AudioGuideDB_DisplayTable.sql` chỉ đọc dữ liệu. Các script tạo và nâng cấp nằm trong `sql/`.
- Bản xem DFD mức 1 nằm trong `Diagram/CNPM_DFD_Muc_1.drawio.png`.

## Kiểm thử và phát hành

```sh
dotnet restore AudioGuideSolution.sln --locked-mode
dotnet build AudioGuideSolution.sln -c Release --no-restore
dotnet test AudioGuide.Tests -c Release --no-build
```

Đặt `AUDIOGUIDE_TEST_SQL` trỏ vào **database kiểm thử riêng đã tạo schema** để bật test tích hợp. CI tạo CSDL kiểm thử riêng trước khi chạy các bài này.

CD chạy theo yêu cầu trên nhánh main sau CI thành công. Cấu hình environment staging, DEPLOY_SSH_KEY, DEPLOY_HOST, DEPLOY_USER và DEPLOY_KNOWN_HOSTS. Host cần thư mục /opt/audioguide, Docker Compose, quyền kéo image GHCR, SQL Server đã chuẩn bị và đã chạy script tài khoản, reverse proxy HTTPS và .env.runtime theo .env.example. Bảo vệ file cấu hình chứa secret. Sao lưu DB trước khi chạy CD. Image rollback không hoàn tác dữ liệu.
