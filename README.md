# 🍽️ Hệ thống đặt món nhà hàng

> Bài tập lớn môn **Lập trình Web** – Trường Đại học Giao thông Vận tải

---

## 📋 Mục lục

- [Giới thiệu](#-giới-thiệu)
- [Nghiệp vụ hệ thống](#-nghiệp-vụ-hệ-thống)
- [Luồng hoạt động chính](#-luồng-hoạt-động-chính)
- [Công nghệ sử dụng](#-công-nghệ-sử-dụng)
- [Cấu trúc dự án](#-cấu-trúc-dự-án)
- [Cài đặt & chạy thử](#-cài-đặt--chạy-thử)
- [Tài khoản demo](#-tài-khoản-demo)
- [Phân công công việc](#-phân-công-công-việc)
- [Tiến độ phát triển](#-tiến-độ-phát-triển)

---

## 🎯 Giới thiệu

Hệ thống hỗ trợ toàn bộ quy trình phục vụ tại nhà hàng — từ lúc khách ngồi xuống bàn đến khi thanh toán — với **4 vai trò** làm việc đồng thời và đồng bộ theo thời gian thực qua **SignalR**.

| Vai trò | Mô tả |
|---|---|
| 👨‍💼 **Admin** | Quản lý toàn bộ hệ thống: menu, bàn, nhân viên, báo cáo, cấu hình |
| 🧑‍🍽️ **Phục vụ (Waiter)** | Mở bàn, chọn món, gửi bếp, theo dõi trạng thái, yêu cầu thanh toán |
| 👨‍🍳 **Bếp (Kitchen)** | Nhận món, đánh dấu đang làm/đã xong, từ chối/báo hết món |
| 💰 **Thu ngân (Cashier)** | Xem danh sách bàn chờ thanh toán, thu tiền, đóng bàn, in hóa đơn |

---

## 📌 Nghiệp vụ hệ thống

### Quản lý thực đơn (Admin)
- CRUD danh mục món ăn và món ăn (tên, mã, giá, ảnh, nhóm ra món)
- Quản lý tùy chọn món (size, topping, sốt…)
- Bật/tắt trạng thái "còn hàng / hết hàng"
- Thay đổi giá được ghi vào nhật ký audit

### Quản lý bàn & khu vực (Admin + Waiter)
- Tổ chức bàn theo khu vực (VIP, thường, ngoài trời…)
- Sơ đồ bàn trực quan, màu sắc phân biệt trạng thái
- Trạng thái bàn: **Trống → Đang phục vụ → Chờ thanh toán → Trống**

### Gọi món & gửi bếp (Waiter)
- Tìm kiếm và chọn món, thêm tùy chọn, ghi chú riêng từng món
- Giỏ nháp lưu tạm bằng Session (chưa gửi bếp)
- Gửi bếp thành đợt (RoundNo) — mỗi lần bấm "Gửi bếp" là một đợt mới
- Món hết hàng tự động bị lọc ra, các món còn lại vẫn gửi được

### Màn hình bếp — KDS (Kitchen)
- 3 cột: **Chờ làm / Đang làm / Đã xong**, sắp xếp theo thời gian chờ
- Cập nhật theo thời gian thực qua SignalR (< 2 giây)
- Bếp có thể từ chối hoặc báo hết món (kèm lý do)
- Hoàn tác bước vừa đánh dấu (`READY → COOKING`, `COOKING → CONFIRMED`)
- Cảnh báo SLA: món quá ngưỡng đổi màu vàng → đỏ

### Sửa / hủy món (Waiter)
- Chỉ sửa/hủy được khi món ở trạng thái **Chờ làm (CONFIRMED)**
- Hủy bắt buộc nhập lý do
- Xử lý xung đột: nếu bếp vừa nhận món đúng lúc phục vụ hủy → báo lỗi 409

### Thanh toán (Cashier)
- Thu ngân thấy danh sách bàn chờ thanh toán theo thời gian thực
- Tạo hóa đơn tự động từ các món hợp lệ (loại bỏ CANCELLED/REJECTED)
- Hỗ trợ thanh toán nhiều lần, nhiều phương thức (Tiền mặt / Thẻ / Chuyển khoản)
- Cảnh báo khi còn món chưa được phục vụ
- In hóa đơn khổ 80mm hoặc A5 qua `@media print`

### Audit log & Báo cáo (Admin)
- Ghi tự động các hành động quan trọng: đăng nhập, đổi giá, bật/tắt món, hủy/sửa/từ chối/hoàn tác món
- Trang audit log chỉ đọc, lọc được theo người/thời gian/loại hành động
- Báo cáo doanh thu theo ngày, top món bán chạy, giờ cao điểm, hiệu suất bếp
- Dashboard tổng quan: doanh thu hôm nay, số bill, bàn phục vụ, món quá SLA

---

## 🔄 Luồng hoạt động chính

```
Phục vụ mở bàn
    → Chọn món → Gửi bếp (CONFIRMED)
        → Bếp nhận (COOKING)
            → Bếp xong (READY)
                → Phục vụ đánh dấu đã phục vụ (SERVED)
    → [Nhiều đợt có thể lặp lại]
    → Phục vụ yêu cầu thanh toán
        → Thu ngân lập hóa đơn
            → Thu ngân thanh toán
                → Bàn về trống
```

Trạng thái món:
```
DRAFT → CONFIRMED → COOKING → READY → SERVED
                                   ↘ CANCELLED  (phục vụ hủy)
             ↘ REJECTED             (bếp từ chối)
```

---

## 🛠️ Công nghệ sử dụng

| Tầng | Công nghệ | Phiên bản |
|---|---|---|
| **Runtime** | .NET | 10 (LTS) |
| **Ngôn ngữ** | C# | 14 |
| **Web Framework** | ASP.NET Core MVC | 10.x |
| **ORM** | Entity Framework Core | 9.0.5 |
| **Cơ sở dữ liệu** | SQL Server | 2022 |
| **Xác thực** | ASP.NET Core Identity | 9.0.5 |
| **Realtime** | ASP.NET Core SignalR | tích hợp sẵn |
| **Logging** | Serilog (Console + File) | 9.0.0 |
| **Frontend** | Bootstrap 5 + jQuery 3.x | CDN |
| **Icons** | Bootstrap Icons | 1.11.3 |
| **Container** | Docker + Docker Compose | — |

### Các NuGet package chính

```xml
<PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="9.0.5" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Tools" Version="9.0.5" />
<PackageReference Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" Version="9.0.5" />
<PackageReference Include="Serilog.AspNetCore" Version="9.0.0" />
<PackageReference Include="Serilog.Sinks.File" Version="7.0.0" />
```

### Kiến trúc tổng thể

```
Trình duyệt (Bootstrap + jQuery + SignalR JS)
      │ HTTP/AJAX          │ WebSocket (SignalR)
      ▼                    ▼
 ASP.NET Core MVC ───── KitchenHub
      │
 Service Layer (Business Logic)
      │
 EF Core ──── SQL Server
      │
 Background Services (SLA Monitor, Reservation Expiry)
```

---

## 📁 Cấu trúc dự án

```
btl_web_c#/
└── btl_web_c#/                  ← Project chính
    ├── Areas/
    │   ├── Admin/               ← Dashboard, CRUD menu/bàn/nhân viên, báo cáo, audit log
    │   ├── Waiter/              ← Sơ đồ bàn, gọi món, theo dõi đơn
    │   ├── Kitchen/             ← Màn hình bếp KDS
    │   └── Cashier/             ← Thanh toán, lịch sử hóa đơn
    ├── Controllers/             ← AccountController, ErrorController
    ├── Data/
    │   ├── AppDbContext.cs
    │   ├── DbInitializer.cs     ← Seed dữ liệu mẫu
    │   └── Interceptors/        ← SoftDeleteInterceptor, AuditInterceptor
    ├── Hubs/
    │   └── KitchenHub.cs        ← SignalR Hub
    ├── Migrations/              ← EF Core Migrations
    ├── Models/
    │   ├── Entities/            ← 24 entity (kế thừa BaseEntity)
    │   └── Enums/
    ├── Services/
    │   ├── AuditService.cs      ← Ghi nhật ký hoạt động
    │   ├── SessionCartService.cs← Giỏ nháp trong Session
    │   ├── SignalRNotifier.cs   ← Wrapper phát sự kiện realtime
    │   └── OverdueItemMonitor.cs← Background service giám sát SLA
    ├── ViewModels/              ← ViewModel theo Area
    ├── Views/
    │   └── Shared/              ← _AdminLayout, _WaiterLayout, _KitchenLayout
    └── wwwroot/                 ← CSS, JS, ảnh tải lên
```

---

## 🚀 Cài đặt & chạy thử

### Yêu cầu

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- SQL Server 2022 (hoặc Docker)
- Visual Studio 2022+ / VS Code / Rider

### Cách 1 – Chạy SQL Server bằng Docker

```bash
# Khởi động SQL Server (nếu chưa có)
docker run -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=Sonns@123" \
  -p 1433:1433 --name sql2022 -d mcr.microsoft.com/mssql/server:2022-latest
```

### Cách 2 – Dùng file docker-compose sẵn có

```bash
cd DH_GTVT/SQL_Server
docker-compose up -d
```

### Chạy ứng dụng

```bash
cd btl_web_c#/btl_web_c#

# Lần đầu: tạo database + seed dữ liệu (chạy tự động khi app khởi động)
dotnet run
```

Mở trình duyệt tại: **`https://localhost:5001`** (hoặc port hiển thị trong console)

### Connection string mặc định

```
Server=localhost,1433;Database=RestaurantDb;User Id=sa;Password=Sonns@123;TrustServerCertificate=True
```

> Thay đổi trong `appsettings.json` nếu cần.

### Tạo migration mới (khi thay đổi entity)

```powershell
$env:PATH = "$env:USERPROFILE\.dotnet\tools;$env:PATH"
dotnet ef migrations add <TenMigration>
dotnet ef database update
```

---

## 🔐 Tài khoản demo

| Tài khoản | Mật khẩu | Vai trò | Đường dẫn sau đăng nhập |
|---|---|---|---|
| `admin` | `Admin@123` | Admin | `/Admin/Dashboard` |
| `waiter1` | `Waiter@123` | Phục vụ | `/Waiter/Tables` |
| `kitchen1` | `Kitchen@123` | Bếp | `/Kitchen/Board` |
| `cashier1` | `Cashier@123` | Thu ngân | `/Cashier/Bills` |

---

## 👥 Phân công công việc

> **Nhóm gồm ... thành viên** – Mỗi thành viên phụ trách một hoặc nhiều giai đoạn phát triển

| Thành viên | MSSV | Phụ trách |
|---|---|---|
| [Thành viên 1] | [...] | GĐ 0, 1 – Khởi tạo dự án, cơ sở dữ liệu, Identity, Layout, phân quyền |
| [Thành viên 2] | [...] | GĐ 2, 3 – Admin CRUD, Sơ đồ bàn, gọi món |
| [Thành viên 3] | [...] | GĐ 4, 5 – Màn hình bếp, SignalR realtime, sửa/hủy món |
| [Thành viên 4] | [...] | GĐ 6, 9 – Thanh toán, audit log, báo cáo, dashboard |

### Chi tiết phân công theo chức năng

| Chức năng | Thành viên |
|---|---|
| Thiết kế CSDL (Entity, DbContext, Migration, Seed) | [Thành viên 1] |
| Xác thực & phân quyền (Identity, Cookie, Policy) | [Thành viên 1] |
| Layout giao diện theo vai trò (4 layouts) | [Thành viên 1] |
| Admin: CRUD Danh mục, Khu vực, Bàn | [Thành viên 2] |
| Admin: CRUD Món ăn (upload ảnh, tùy chọn) | [Thành viên 2] |
| Admin: CRUD Nhân viên + Cấu hình hệ thống | [Thành viên 2] |
| Sơ đồ bàn & mở bàn (RowVersion chống xung đột) | [Thành viên 2] |
| Chọn món & giỏ nháp (Session) | [Thành viên 2] |
| Gửi bếp theo đợt (RoundNo) | [Thành viên 2] |
| Màn hình bếp KDS (3 cột) | [Thành viên 3] |
| SignalR Hub, Groups, IRealtimeNotifier | [Thành viên 3] |
| Realtime client JS (kết nối, reconnect, banner) | [Thành viên 3] |
| Hủy / sửa món (ORD-02, ORD-03), xử lý 409 | [Thành viên 3] |
| Bếp từ chối / hoàn tác (KIT-05, KIT-06) | [Thành viên 3] |
| SLA Monitor, cảnh báo màu đỏ/vàng | [Thành viên 3] |
| Thanh toán nhiều lần, đóng bàn (GĐ 6) | [Thành viên 4] |
| In hóa đơn (@media print) | [Thành viên 4] |
| Audit log (ghi tự động, trang xem) | [Thành viên 4] |
| Lịch sử hóa đơn (lọc/phân trang) | [Thành viên 4] |
| Dashboard (số liệu thực) | [Thành viên 4] |
| Báo cáo: doanh thu, top món, giờ cao điểm, hiệu suất bếp | [Thành viên 4] |

---

## 📈 Tiến độ phát triển

| Giai đoạn | Nội dung | Trạng thái |
|---|---|---|
| GĐ 0 | Khởi tạo dự án, CSDL, seed dữ liệu | ✅ Hoàn thành |
| GĐ 1 | Layout, đăng nhập, phân quyền 4 vai trò | ✅ Hoàn thành |
| GĐ 2 | Admin CRUD (menu, bàn, nhân viên, cài đặt) | ✅ Hoàn thành |
| GĐ 3 | Sơ đồ bàn & gọi món | ✅ Hoàn thành |
| GĐ 4 | Màn hình bếp KDS & SignalR realtime | ✅ Hoàn thành |
| GĐ 5 | Sửa/hủy món, hết món, xử lý xung đột | ✅ Hoàn thành |
| GĐ 6 | Thanh toán MVP (bill, thu tiền, in HĐ) | ✅ Hoàn thành |
| GĐ 7 | Đặt bàn, chuyển & gộp bàn | 🔄 Đang phát triển |
| GĐ 8 | Nâng cao bếp (SLA, ưu tiên, từ chối, hoàn tác) | 🔄 Đang phát triển |
| GĐ 9 | Audit log, lịch sử bill, báo cáo, dashboard | ✅ Hoàn thành |
| GĐ 10 | Quốc tế hóa, bảo mật, Docker, đóng gói | ⏳ Chưa bắt đầu |

---

## 📄 Tài liệu

| File | Nội dung |
|---|---|
| `doc/nghiep-vu-he-thong-dat-mon.md` | Đặc tả nghiệp vụ chi tiết (use case, luồng xử lý) |
| `doc/thiet-ke-cong-nghe-he-thong-dat-mon.md` | Thiết kế công nghệ, kiến trúc, API, bảo mật |
| `doc/thiet-ke-co-so-du-lieu.md` | Thiết kế CSDL (ERD, bảng, quan hệ) |
| `doc/thiet-ke-giao-dien-he-thong-dat-mon.md` | Wireframe & thiết kế giao diện từng màn hình |
| `doc/plan-phat-trien-he-thong-dat-mon.md` | Kế hoạch phát triển theo giai đoạn |

---

## 🔑 Điểm kỹ thuật nổi bật

- **Soft delete toàn cục** — `HasQueryFilter(!IsDeleted)` trên mọi entity, interceptor tự xử lý khi `Remove()`
- **Concurrency control** — `RowVersion` trên `CartItems`, trả 409 khi xung đột (phục vụ hủy vs bếp nhận)
- **Snapshot giá** — tên/giá/tùy chọn được snapshot vào `CartItems` khi gửi bếp, không bị ảnh hưởng khi admin đổi giá sau
- **Session cart** — giỏ nháp lưu trong `ISession`, chỉ tạo `CartItems` thực sự khi bấm "Gửi bếp"
- **Realtime pattern** — `IRealtimeNotifier` → `SignalRNotifier`, sự kiện chỉ phát sau `SaveChanges` thành công
- **Audit logging** — `IAuditService` ghi đầy đủ `OldValues`/`NewValues` dạng JSON cho các hành động quan trọng
- **Background services** — `OverdueItemMonitor` (30s) giám sát SLA, `ReservationExpiryService` (1 phút) nhả bàn quá giờ

---

<p align="center">
  Dự án bài tập lớn môn Lập trình Web – Trường Đại học Giao thông Vận tải
</p>
