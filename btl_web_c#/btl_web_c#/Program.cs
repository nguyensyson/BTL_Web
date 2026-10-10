using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RestaurantOrdering.Data;
using RestaurantOrdering.Data.Interceptors;
using RestaurantOrdering.Models.Entities;
using Serilog;

// ── Serilog bootstrap logger (bắt lỗi khởi động) ─────────────────────────
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Khởi động ứng dụng...");

    var builder = WebApplication.CreateBuilder(args);

    // ── Serilog từ appsettings ────────────────────────────────────────────
    builder.Host.UseSerilog((ctx, lc) =>
        lc.ReadFrom.Configuration(ctx.Configuration));

    // ── Interceptors (cần đăng ký trước DbContext) ────────────────────────
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<AuditInterceptor>();
    builder.Services.AddSingleton<SoftDeleteInterceptor>();

    // ── EF Core + SQL Server ─────────────────────────────────────────────
    builder.Services.AddDbContext<AppDbContext>((sp, options) =>
    {
        options.UseSqlServer(
            builder.Configuration.GetConnectionString("Default"),
            sql => sql.EnableRetryOnFailure(3));

        options.AddInterceptors(
            sp.GetRequiredService<SoftDeleteInterceptor>(),
            sp.GetRequiredService<AuditInterceptor>());
    });

    // ── ASP.NET Core Identity ─────────────────────────────────────────────
    builder.Services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
    {
        options.Password.RequiredLength = 6;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.User.RequireUniqueEmail = false;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

    // ── Cookie xác thực ──────────────────────────────────────────────────
    builder.Services.ConfigureApplicationCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Error/403";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
    });

    // ── Phân quyền (Policy) ───────────────────────────────────────────────
    builder.Services.AddAuthorizationBuilder()
        .AddPolicy("CanManageOrders",  p => p.RequireRole("Waiter", "Admin"))
        .AddPolicy("CanUpdateCooking", p => p.RequireRole("Kitchen", "Admin"))
        .AddPolicy("CanPay",           p => p.RequireRole("Cashier", "Admin"))
        .AddPolicy("AdminOnly",        p => p.RequireRole("Admin"));

    // ── MVC + Razor Views ─────────────────────────────────────────────────
    builder.Services.AddControllersWithViews(options =>
    {
        // Auto validate antiforgery cho tất cả POST/PUT/DELETE
        options.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute());
    });

    // ── Session (lưu giỏ order nháp) ─────────────────────────────────────
    builder.Services.AddDistributedMemoryCache();
    builder.Services.AddSession(options =>
    {
        options.IdleTimeout = TimeSpan.FromMinutes(60);
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
    });

    // ── SignalR ───────────────────────────────────────────────────────────
    builder.Services.AddSignalR();

    // ── App Services ──────────────────────────────────────────────────────
    builder.Services.AddScoped<RestaurantOrdering.Services.SessionCartService>();
    builder.Services.AddScoped<RestaurantOrdering.Services.IRealtimeNotifier,
                                RestaurantOrdering.Services.SignalRNotifier>();
    builder.Services.AddScoped<RestaurantOrdering.Services.IAuditService,
                                RestaurantOrdering.Services.AuditService>();

    // ── Background Services ──────────────────────────────────────────────
    builder.Services.AddHostedService<RestaurantOrdering.Services.OverdueItemMonitor>();
    builder.Services.AddHostedService<RestaurantOrdering.Services.ReservationExpiryService>();

    // ── Build app ─────────────────────────────────────────────────────────
    var app = builder.Build();

    // ── Middleware pipeline ───────────────────────────────────────────────
    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error/500");
        app.UseHsts();
    }
    else
    {
        app.UseDeveloperExceptionPage();
    }

    app.UseHttpsRedirection();
    app.UseStaticFiles();
    app.UseSerilogRequestLogging();

    app.UseRouting();

    app.UseSession();

    app.UseAuthentication();
    app.UseAuthorization();

    // ── Routes ────────────────────────────────────────────────────────────
    app.MapControllerRoute(
        name: "areas",
        pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}");

    // ── SignalR Hub ───────────────────────────────────────────────────────
    app.MapHub<RestaurantOrdering.Hubs.KitchenHub>("/hubs/kitchen");

    // ── Seed dữ liệu ─────────────────────────────────────────────────────
    await DbInitializer.SeedAsync(app.Services);

    Log.Information("Ứng dụng đã sẵn sàng.");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Ứng dụng khởi động thất bại.");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
