# WindowsTimeSync

**WindowsTimeSync** là tiện ích nhẹ giúp tự động hóa, cấu hình và khắc phục sự cố đồng bộ thời gian (NTP/W32Time) trên hệ điều hành Windows, đặc biệt hữu ích cho máy tính cá nhân, máy ảo hoặc hệ thống dual-boot (Windows/Linux).

---

## 🚀 Tính Năng Chính

- **Đồng Bộ Nhanh (Force Resync):** Kích hoạt cập nhật thời gian tức thì từ máy chủ NTP chỉ bằng một thao tác.
- **Tùy Chỉnh Máy Chủ NTP:** Dễ dàng chuyển đổi hoặc thêm máy chủ thời gian đáng tin cậy (`pool.ntp.org`, `time.windows.com`, `time.google.com`, `time.cloudflare.com`).
- **Sửa Lỗi Lệch Giờ Dual-Boot:** Tùy chọn chuyển đổi định dạng lưu trữ phần cứng giữa Local Time và UTC (`RealTimeIsUniversal`).
- **Sửa Chữa Dịch Vụ (Self-Heal W32Time):** Tự động khởi động lại, đăng ký lại tệp thực thi (`w32tm /register`) và reset cấu hình dịch vụ Windows Time về mặc định khi bị lỗi.
- **Tự Động Hóa:** Hỗ trợ tạo tác vụ tự động đồng bộ định kỳ qua Windows Task Scheduler.

---

## 📋 Yêu Cầu Hệ Thống

- **Hệ điều hành:** Windows 10 / 11 / Windows Server 2016+.
- **Quyền hạn:** Cần quyền Quản trị viên (**Run as Administrator**) để can thiệp vào dịch vụ hệ thống và Registry.
- **Môi trường:** PowerShell 5.1+ / .NET Runtime (nếu dùng bản dựng GUI).

---

## 🛠️ Cài Đặt

### Cách 1: Tải Bản Phát Hành Sẵn
1. Truy cập mục [Releases](https://github.com/your-username/WindowsTimeSync/releases).
2. Tải về file nén mới nhất (`WindowsTimeSync.zip`).
3. Giải nén vào thư mục cố định (ví dụ: `C:\Tools\WindowsTimeSync`).

### Cách 2: Chạy Qua PowerShell Trực Tiếp
```powershell
# Chạy với quyền Administrator
Set-ExecutionPolicy Bypass -Scope Process -Force
.\WindowsTimeSync.ps1

📖 Hướng Dẫn Sử Dụng
Lưu ý: Luôn chạy ứng dụng/script bằng Run as Administrator.

1. Dòng Lệnh Cơ Bản (CLI)
Đồng bộ thời gian ngay lập tức:

DOS
windowstimesync --sync
Kiểm tra trạng thái đồng bộ hiện tại:

DOS
windowstimesync --status
Đặt máy chủ NTP tùy chỉnh (ví dụ Cloudflare & Google):

DOS
windowstimesync --set-ntp "time.cloudflare.com,0x1 time.google.com,0x1"
Khắc phục sự cố lệch giờ Dual-Boot (bật UTC cho BIOS/RTC):

DOS
windowstimesync --fix-rtc
Reset toàn bộ dịch vụ Windows Time về mặc định:

DOS
windowstimesync --repair
2. Tự Động Hóa Đồng Bộ Định Kỳ
Để thiết lập đồng bộ mỗi khi khởi động hoặc mỗi 6 giờ:

PowerShell
.\WindowsTimeSync.ps1 -InstallSchedule -IntervalHours 6
⚙️ Cấu Trúc File Cấu Hình (config.json)
JSON
{
  "default_ntp_servers": [
    "time.cloudflare.com,0x1",
    "pool.ntp.org,0x1",
    "time.google.com,0x1"
  ],
  "sync_interval_hours": 6,
  "use_utc_hardware_clock": false,
  "auto_repair_on_failure": true
}
🔍 Khắc Phục Sự Cố Thường Gặp
Lỗi Access Denied: Bạn chưa mở ứng dụng/Terminal bằng quyền Administrator.

Lỗi The computer did not resync because no time data was available: Cổng UDP 123 có thể đang bị chặn bởi tường lửa mạng hoặc máy chủ NTP bạn chọn không phản hồi.

🤝 Đóng Góp (Contributing)
Mọi đóng góp nhằm tối ưu hóa script hoặc phát triển giao diện đều được hoan nghênh:

Fork dự án.

Tạo branch mới (git checkout -b feature/cai-tien).

Commit mã nguồn (git commit -m 'Thêm tính năng...').

Push và mở Pull Request.

📄 Bản Quyền (License)
Dự án được phân phối theo giấy phép MIT License.
