using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RestaurantOrdering.Models.Entities;

namespace RestaurantOrdering.Data;

public static class DbInitializer
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();

        await db.Database.MigrateAsync();

        await SeedRolesAsync(roleManager);
        await SeedStatusesAsync(db);
        await SeedPaymentMethodsAsync(db);
        await SeedSystemSettingsAsync(db);
        await SeedAreasAndTablesAsync(db);
        await SeedCategoriesAndProductsAsync(db);
        await SeedUsersAsync(userManager);
    }

    // ────────────────────────────────────────────────────────────────────────
    private static async Task SeedRolesAsync(RoleManager<ApplicationRole> roleManager)
    {
        string[] roles = ["Admin", "Waiter", "Kitchen", "Cashier"];
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new ApplicationRole(role));
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    private static async Task SeedStatusesAsync(AppDbContext db)
    {
        if (!await db.TableStatuses.IgnoreQueryFilters().AnyAsync())
        {
            db.TableStatuses.AddRange(
                new TableStatus { Id = 1, Code = "AVAILABLE", Name = "Trống", ColorHex = "#2ECC71" },
                new TableStatus { Id = 2, Code = "OCCUPIED", Name = "Đang phục vụ", ColorHex = "#E67E22" },
                new TableStatus { Id = 3, Code = "WAITING_PAYMENT", Name = "Chờ thanh toán", ColorHex = "#E74C3C" },
                new TableStatus { Id = 4, Code = "RESERVED", Name = "Đã đặt trước", ColorHex = "#3498DB" }
            );
        }

        if (!await db.CartStatuses.IgnoreQueryFilters().AnyAsync())
        {
            db.CartStatuses.AddRange(
                new CartStatus { Id = 1, Code = "OPEN", Name = "Đang gọi món" },
                new CartStatus { Id = 2, Code = "WAITING_PAYMENT", Name = "Chờ thanh toán" },
                new CartStatus { Id = 3, Code = "PAID", Name = "Đã thanh toán" },
                new CartStatus { Id = 4, Code = "CANCELLED", Name = "Đã hủy" }
            );
        }

        if (!await db.CartItemStatuses.IgnoreQueryFilters().AnyAsync())
        {
            db.CartItemStatuses.AddRange(
                new CartItemStatus { Id = 1, Code = "DRAFT", Name = "Nháp", ColorHex = "#95A5A6", ShowOnKitchen = false },
                new CartItemStatus { Id = 2, Code = "CONFIRMED", Name = "Chờ làm", ColorHex = "#F1C40F", ShowOnKitchen = true },
                new CartItemStatus { Id = 3, Code = "COOKING", Name = "Đang làm", ColorHex = "#E67E22", ShowOnKitchen = true },
                new CartItemStatus { Id = 4, Code = "READY", Name = "Đã xong", ColorHex = "#2ECC71", ShowOnKitchen = true },
                new CartItemStatus { Id = 5, Code = "SERVED", Name = "Đã phục vụ", ColorHex = "#3498DB", ShowOnKitchen = false },
                new CartItemStatus { Id = 6, Code = "CANCELLED", Name = "Đã hủy", ColorHex = "#7F8C8D", ShowOnKitchen = false },
                new CartItemStatus { Id = 7, Code = "REJECTED", Name = "Bếp từ chối", ColorHex = "#C0392B", ShowOnKitchen = false }
            );
        }

        await db.SaveChangesAsync();
    }

    // ────────────────────────────────────────────────────────────────────────
    private static async Task SeedPaymentMethodsAsync(AppDbContext db)
    {
        if (await db.PaymentMethods.IgnoreQueryFilters().AnyAsync()) return;

        db.PaymentMethods.AddRange(
            new PaymentMethod { Id = 1, Code = "CASH", Name = "Tiền mặt" },
            new PaymentMethod { Id = 2, Code = "TRANSFER", Name = "Chuyển khoản" },
            new PaymentMethod { Id = 3, Code = "CARD", Name = "Thẻ" }
        );
        await db.SaveChangesAsync();
    }

    // ────────────────────────────────────────────────────────────────────────
    private static async Task SeedSystemSettingsAsync(AppDbContext db)
    {
        if (await db.SystemSettings.IgnoreQueryFilters().AnyAsync()) return;

        db.SystemSettings.AddRange(
            new SystemSetting { SettingKey = "TaxRate", SettingValue = "0.08", Description = "Thuế VAT (0.08 = 8%)" },
            new SystemSetting { SettingKey = "ServiceFeeRate", SettingValue = "0.05", Description = "Phí dịch vụ (0.05 = 5%)" },
            new SystemSetting { SettingKey = "ItemSlaMinutes", SettingValue = "15", Description = "Ngưỡng SLA món ăn (phút)" },
            new SystemSetting { SettingKey = "ReservationHoldMinutes", SettingValue = "20", Description = "Thời gian giữ bàn đặt trước (phút)" },
            new SystemSetting { SettingKey = "RestaurantName", SettingValue = "Nhà hàng Mặt Trời", Description = "Tên nhà hàng in trên hóa đơn" },
            new SystemSetting { SettingKey = "RestaurantAddress", SettingValue = "123 Đường ABC, Hà Nội", Description = "Địa chỉ nhà hàng" },
            new SystemSetting { SettingKey = "RestaurantPhone", SettingValue = "024-1234-5678", Description = "SĐT nhà hàng" }
        );
        await db.SaveChangesAsync();
    }

    // ────────────────────────────────────────────────────────────────────────
    private static async Task SeedAreasAndTablesAsync(AppDbContext db)
    {
        if (await db.Areas.IgnoreQueryFilters().AnyAsync()) return;

        var areaA = new Area { Name = "Khu vực trong nhà", SortOrder = 1 };
        var areaB = new Area { Name = "Khu vực ngoài trời", SortOrder = 2 };
        db.Areas.AddRange(areaA, areaB);
        await db.SaveChangesAsync();

        var tables = new List<DiningTable>();
        // Khu trong nhà: T01–T08
        for (int i = 1; i <= 8; i++)
            tables.Add(new DiningTable
            {
                Code = $"T{i:D2}",
                AreaId = areaA.Id,
                Capacity = i <= 4 ? 4 : 6,
                StatusId = 1,  // AVAILABLE
                SortOrder = i
            });

        // Khu ngoài trời: T09–T12
        for (int i = 9; i <= 12; i++)
            tables.Add(new DiningTable
            {
                Code = $"T{i:D2}",
                AreaId = areaB.Id,
                Capacity = 4,
                StatusId = 1,
                SortOrder = i
            });

        db.DiningTables.AddRange(tables);
        await db.SaveChangesAsync();
    }

    // ────────────────────────────────────────────────────────────────────────
    private static async Task SeedCategoriesAndProductsAsync(AppDbContext db)
    {
        if (await db.Categories.IgnoreQueryFilters().AnyAsync()) return;

        var catKhaiVi = new Category { Name = "Khai vị", SortOrder = 1 };
        var catMonChinh = new Category { Name = "Món chính", SortOrder = 2 };
        var catTrangMieng = new Category { Name = "Tráng miệng", SortOrder = 3 };
        var catDoUong = new Category { Name = "Đồ uống", SortOrder = 4 };
        db.Categories.AddRange(catKhaiVi, catMonChinh, catTrangMieng, catDoUong);
        await db.SaveChangesAsync();

        var products = new List<Product>
        {
            // Khai vị
            new() { CategoryId = catKhaiVi.Id, Code = "KV001", Name = "Gỏi cuốn tôm thịt", Price = 45000, CourseGroup = 1, SortOrder = 1 },
            new() { CategoryId = catKhaiVi.Id, Code = "KV002", Name = "Chả giò chiên giòn", Price = 55000, CourseGroup = 1, SortOrder = 2 },
            new() { CategoryId = catKhaiVi.Id, Code = "KV003", Name = "Súp cua bắp", Price = 40000, CourseGroup = 1, SortOrder = 3 },
            new() { CategoryId = catKhaiVi.Id, Code = "KV004", Name = "Gỏi ngó sen tôm thịt", Price = 65000, CourseGroup = 1, SortOrder = 4 },
            // Món chính
            new() { CategoryId = catMonChinh.Id, Code = "MC001", Name = "Bò lúc lắc", Price = 185000, CourseGroup = 2, SortOrder = 1 },
            new() { CategoryId = catMonChinh.Id, Code = "MC002", Name = "Cơm chiên dương châu", Price = 75000, CourseGroup = 2, SortOrder = 2 },
            new() { CategoryId = catMonChinh.Id, Code = "MC003", Name = "Mì xào hải sản", Price = 95000, CourseGroup = 2, SortOrder = 3 },
            new() { CategoryId = catMonChinh.Id, Code = "MC004", Name = "Lẩu thái hải sản (2 người)", Price = 320000, CourseGroup = 2, SortOrder = 4 },
            new() { CategoryId = catMonChinh.Id, Code = "MC005", Name = "Cá hồi áp chảo", Price = 245000, CourseGroup = 2, SortOrder = 5 },
            new() { CategoryId = catMonChinh.Id, Code = "MC006", Name = "Sườn non nướng BBQ", Price = 165000, CourseGroup = 2, SortOrder = 6 },
            new() { CategoryId = catMonChinh.Id, Code = "MC007", Name = "Tôm rang me", Price = 195000, CourseGroup = 2, SortOrder = 7 },
            new() { CategoryId = catMonChinh.Id, Code = "MC008", Name = "Cơm tấm sườn trứng", Price = 85000, CourseGroup = 2, SortOrder = 8 },
            // Tráng miệng
            new() { CategoryId = catTrangMieng.Id, Code = "TM001", Name = "Chè thái bốn màu", Price = 35000, CourseGroup = 3, SortOrder = 1 },
            new() { CategoryId = catTrangMieng.Id, Code = "TM002", Name = "Kem dừa tươi", Price = 45000, CourseGroup = 3, SortOrder = 2 },
            new() { CategoryId = catTrangMieng.Id, Code = "TM003", Name = "Bánh flan caramel", Price = 30000, CourseGroup = 3, SortOrder = 3 },
            // Đồ uống
            new() { CategoryId = catDoUong.Id, Code = "DU001", Name = "Nước ngọt lon", Price = 20000, CourseGroup = 2, SortOrder = 1 },
            new() { CategoryId = catDoUong.Id, Code = "DU002", Name = "Bia lon (Heineken/Tiger)", Price = 35000, CourseGroup = 2, SortOrder = 2 },
            new() { CategoryId = catDoUong.Id, Code = "DU003", Name = "Nước ép cam tươi", Price = 45000, CourseGroup = 2, SortOrder = 3 },
            new() { CategoryId = catDoUong.Id, Code = "DU004", Name = "Cà phê đen", Price = 30000, CourseGroup = 2, SortOrder = 4 },
            new() { CategoryId = catDoUong.Id, Code = "DU005", Name = "Trà đào cam sả", Price = 45000, CourseGroup = 2, SortOrder = 5 },
        };
        db.Products.AddRange(products);
        await db.SaveChangesAsync();

        // Tùy chọn cho một số món
        var boLucLac = products.First(p => p.Code == "MC001");
        var nuocNgot = products.First(p => p.Code == "DU001");

        db.ProductItemOptions.AddRange(
            // Bò lúc lắc: độ chín
            new ProductItemOption { ProductId = boLucLac.Id, GroupName = "Độ chín", Name = "Tái", ExtraPrice = 0, SortOrder = 1 },
            new ProductItemOption { ProductId = boLucLac.Id, GroupName = "Độ chín", Name = "Chín vừa", ExtraPrice = 0, SortOrder = 2 },
            new ProductItemOption { ProductId = boLucLac.Id, GroupName = "Độ chín", Name = "Chín kỹ", ExtraPrice = 0, SortOrder = 3 },
            // Nước ngọt: loại
            new ProductItemOption { ProductId = nuocNgot.Id, GroupName = "Loại", Name = "Coca Cola", ExtraPrice = 0, SortOrder = 1 },
            new ProductItemOption { ProductId = nuocNgot.Id, GroupName = "Loại", Name = "Pepsi", ExtraPrice = 0, SortOrder = 2 },
            new ProductItemOption { ProductId = nuocNgot.Id, GroupName = "Loại", Name = "Sprite", ExtraPrice = 0, SortOrder = 3 },
            new ProductItemOption { ProductId = nuocNgot.Id, GroupName = "Loại", Name = "Fanta cam", ExtraPrice = 0, SortOrder = 4 }
        );
        await db.SaveChangesAsync();
    }

    // ────────────────────────────────────────────────────────────────────────
    private static async Task SeedUsersAsync(UserManager<ApplicationUser> userManager)
    {
        var users = new[]
        {
            new { UserName = "admin", FullName = "Quản trị viên", Role = "Admin", Password = "Admin@123" },
            new { UserName = "waiter1", FullName = "Nguyễn Phục Vụ", Role = "Waiter", Password = "Waiter@123" },
            new { UserName = "kitchen1", FullName = "Trần Đầu Bếp", Role = "Kitchen", Password = "Kitchen@123" },
            new { UserName = "cashier1", FullName = "Lê Thu Ngân", Role = "Cashier", Password = "Cashier@123" },
        };

        foreach (var u in users)
        {
            if (await userManager.FindByNameAsync(u.UserName) is null)
            {
                var user = new ApplicationUser
                {
                    UserName = u.UserName,
                    FullName = u.FullName,
                    Email = $"{u.UserName}@restaurant.local",
                    EmailConfirmed = true,
                    IsActive = true
                };
                var result = await userManager.CreateAsync(user, u.Password);
                if (result.Succeeded)
                    await userManager.AddToRoleAsync(user, u.Role);
            }
        }
    }
}
