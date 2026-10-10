using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RestaurantOrdering.Models.Entities;

namespace RestaurantOrdering.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, int,
    IdentityUserClaim<int>, IdentityUserRole<int>, IdentityUserLogin<int>,
    IdentityRoleClaim<int>, IdentityUserToken<int>>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Area> Areas => Set<Area>();
    public DbSet<TableStatus> TableStatuses => Set<TableStatus>();
    public DbSet<DiningTable> DiningTables => Set<DiningTable>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<ReservationTable> ReservationTables => Set<ReservationTable>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductItemOption> ProductItemOptions => Set<ProductItemOption>();
    public DbSet<CartStatus> CartStatuses => Set<CartStatus>();
    public DbSet<CartItemStatus> CartItemStatuses => Set<CartItemStatus>();
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<UsedTable> UsedTables => Set<UsedTable>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<CartItemOption> CartItemOptions => Set<CartItemOption>();
    public DbSet<CartItemStatusHistory> CartItemStatusHistories => Set<CartItemStatusHistory>();
    public DbSet<Bill> Bills => Set<Bill>();
    public DbSet<BillDetail> BillDetails => Set<BillDetail>();
    public DbSet<PaymentMethod> PaymentMethods => Set<PaymentMethod>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // ── Rename Identity tables ──────────────────────────────────────────
        builder.Entity<ApplicationUser>().ToTable("Users");
        builder.Entity<ApplicationRole>().ToTable("Roles");
        builder.Entity<IdentityUserRole<int>>().ToTable("UserRoles");
        builder.Entity<IdentityUserClaim<int>>().ToTable("UserClaims");
        builder.Entity<IdentityUserLogin<int>>().ToTable("UserLogins");
        builder.Entity<IdentityUserToken<int>>().ToTable("UserTokens");
        builder.Entity<IdentityRoleClaim<int>>().ToTable("RoleClaims");

        // ── Global query filters (soft delete) ─────────────────────────────
        builder.Entity<Area>().HasQueryFilter(x => !x.IsDeleted);
        builder.Entity<DiningTable>().HasQueryFilter(x => !x.IsDeleted);
        builder.Entity<TableStatus>().HasQueryFilter(x => !x.IsDeleted);
        builder.Entity<Reservation>().HasQueryFilter(x => !x.IsDeleted);
        builder.Entity<ReservationTable>().HasQueryFilter(x => !x.IsDeleted);
        builder.Entity<Category>().HasQueryFilter(x => !x.IsDeleted);
        builder.Entity<Product>().HasQueryFilter(x => !x.IsDeleted);
        builder.Entity<ProductItemOption>().HasQueryFilter(x => !x.IsDeleted);
        builder.Entity<CartStatus>().HasQueryFilter(x => !x.IsDeleted);
        builder.Entity<CartItemStatus>().HasQueryFilter(x => !x.IsDeleted);
        builder.Entity<Cart>().HasQueryFilter(x => !x.IsDeleted);
        builder.Entity<UsedTable>().HasQueryFilter(x => !x.IsDeleted);
        builder.Entity<CartItem>().HasQueryFilter(x => !x.IsDeleted);
        builder.Entity<CartItemOption>().HasQueryFilter(x => !x.IsDeleted);
        builder.Entity<Bill>().HasQueryFilter(x => !x.IsDeleted);
        builder.Entity<BillDetail>().HasQueryFilter(x => !x.IsDeleted);
        builder.Entity<Payment>().HasQueryFilter(x => !x.IsDeleted);
        builder.Entity<SystemSetting>().HasQueryFilter(x => !x.IsDeleted);
        builder.Entity<ApplicationUser>().HasQueryFilter(x => !x.IsDeleted);

        // ── ApplicationUser ─────────────────────────────────────────────────
        builder.Entity<ApplicationUser>(e =>
        {
            e.Property(x => x.FullName).HasMaxLength(100).IsRequired();
            e.Property(x => x.Phone).HasMaxLength(20);
        });

        // ── Area ─────────────────────────────────────────────────────────────
        builder.Entity<Area>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.HasIndex(x => x.Name).IsUnique().HasFilter("[IsDeleted] = 0");
        });

        // ── TableStatus ──────────────────────────────────────────────────────
        builder.Entity<TableStatus>(e =>
        {
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Code).HasMaxLength(30).IsRequired();
            e.Property(x => x.Name).HasMaxLength(50).IsRequired();
            e.Property(x => x.ColorHex).HasMaxLength(7);
            e.HasIndex(x => x.Code).IsUnique().HasFilter("[IsDeleted] = 0");
        });

        // ── DiningTable ──────────────────────────────────────────────────────
        builder.Entity<DiningTable>(e =>
        {
            e.Property(x => x.Code).HasMaxLength(20).IsRequired();
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasIndex(x => x.Code).IsUnique().HasFilter("[IsDeleted] = 0");
            e.HasOne(x => x.TableStatus).WithMany(x => x.DiningTables)
                .HasForeignKey(x => x.StatusId);
        });

        // ── Reservation ──────────────────────────────────────────────────────
        builder.Entity<Reservation>(e =>
        {
            e.Property(x => x.CustomerName).HasMaxLength(100).IsRequired();
            e.Property(x => x.Phone).HasMaxLength(20).IsRequired();
            e.Property(x => x.Note).HasMaxLength(500);
        });

        // ── ReservationTable ─────────────────────────────────────────────────
        builder.Entity<ReservationTable>(e =>
        {
            e.HasIndex(x => new { x.ReservationId, x.TableId }).IsUnique()
                .HasFilter("[IsDeleted] = 0");
        });

        // ── Category ─────────────────────────────────────────────────────────
        builder.Entity<Category>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.Description).HasMaxLength(255);
            e.HasIndex(x => x.Name).IsUnique().HasFilter("[IsDeleted] = 0");
        });

        // ── Product ──────────────────────────────────────────────────────────
        builder.Entity<Product>(e =>
        {
            e.Property(x => x.Code).HasMaxLength(30).IsRequired();
            e.Property(x => x.Name).HasMaxLength(150).IsRequired();
            e.Property(x => x.Price).HasColumnType("decimal(18,2)");
            e.Property(x => x.ImageUrl).HasMaxLength(255);
            e.Property(x => x.Description).HasMaxLength(500);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasIndex(x => x.Code).IsUnique().HasFilter("[IsDeleted] = 0");
        });

        // ── ProductItemOption ────────────────────────────────────────────────
        builder.Entity<ProductItemOption>(e =>
        {
            e.Property(x => x.GroupName).HasMaxLength(50);
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.ExtraPrice).HasColumnType("decimal(18,2)");
        });

        // ── CartStatus ───────────────────────────────────────────────────────
        builder.Entity<CartStatus>(e =>
        {
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Code).HasMaxLength(30).IsRequired();
            e.Property(x => x.Name).HasMaxLength(50).IsRequired();
            e.HasIndex(x => x.Code).IsUnique().HasFilter("[IsDeleted] = 0");
        });

        // ── CartItemStatus ───────────────────────────────────────────────────
        builder.Entity<CartItemStatus>(e =>
        {
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Code).HasMaxLength(30).IsRequired();
            e.Property(x => x.Name).HasMaxLength(50).IsRequired();
            e.Property(x => x.ColorHex).HasMaxLength(7);
            e.HasIndex(x => x.Code).IsUnique().HasFilter("[IsDeleted] = 0");
        });

        // ── Cart ─────────────────────────────────────────────────────────────
        builder.Entity<Cart>(e =>
        {
            e.Property(x => x.CartCode).HasMaxLength(30).IsRequired();
            e.Property(x => x.Note).HasMaxLength(500);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasIndex(x => x.CartCode).IsUnique().HasFilter("[IsDeleted] = 0");
            e.HasOne(x => x.Waiter).WithMany(x => x.Carts)
                .HasForeignKey(x => x.WaiterId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.CartStatus).WithMany(x => x.Carts)
                .HasForeignKey(x => x.StatusId);
        });

        // ── UsedTable ────────────────────────────────────────────────────────
        builder.Entity<UsedTable>(e =>
        {
            // Filtered unique: một bàn chỉ thuộc một cart đang hoạt động
            e.HasIndex(x => x.TableId).IsUnique()
                .HasFilter("[EndTime] IS NULL AND [IsDeleted] = 0");
        });

        // ── CartItem ─────────────────────────────────────────────────────────
        builder.Entity<CartItem>(e =>
        {
            e.Property(x => x.ProductName).HasMaxLength(150).IsRequired();
            e.Property(x => x.UnitPrice).HasColumnType("decimal(18,2)");
            e.Property(x => x.Note).HasMaxLength(255);
            e.Property(x => x.CancelReason).HasMaxLength(255);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasOne(x => x.CartItemStatus).WithMany(x => x.CartItems)
                .HasForeignKey(x => x.StatusId);
            e.HasOne(x => x.Cart).WithMany(x => x.CartItems)
                .HasForeignKey(x => x.CartId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne(x => x.Product).WithMany(x => x.CartItems)
                .HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.NoAction);
        });

        // ── CartItemOption ───────────────────────────────────────────────────
        builder.Entity<CartItemOption>(e =>
        {
            e.Property(x => x.OptionName).HasMaxLength(100).IsRequired();
            e.Property(x => x.ExtraPrice).HasColumnType("decimal(18,2)");
            e.HasOne(x => x.CartItem).WithMany(x => x.CartItemOptions)
                .HasForeignKey(x => x.CartItemId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne(x => x.ProductItemOption).WithMany(x => x.CartItemOptions)
                .HasForeignKey(x => x.ProductItemOptionId).OnDelete(DeleteBehavior.NoAction);
        });

        // ── CartItemStatusHistory ─────────────────────────────────────────────
        builder.Entity<CartItemStatusHistory>(e =>
        {
            e.HasOne(x => x.CartItem).WithMany(x => x.CartItemStatusHistories)
                .HasForeignKey(x => x.CartItemId).OnDelete(DeleteBehavior.NoAction)
                .IsRequired(false);  // Optional để tránh filter warning
        });
        // Thêm query filter cho CartItemStatusHistory để khớp với CartItem
        builder.Entity<CartItemStatusHistory>().HasQueryFilter(x => !x.IsDeleted);

        // ── Bill ─────────────────────────────────────────────────────────────
        builder.Entity<Bill>(e =>
        {
            e.Property(x => x.BillNo).HasMaxLength(30).IsRequired();
            e.Property(x => x.TableNames).HasMaxLength(200);
            e.Property(x => x.SubTotal).HasColumnType("decimal(18,2)");
            e.Property(x => x.ServiceFee).HasColumnType("decimal(18,2)");
            e.Property(x => x.TaxAmount).HasColumnType("decimal(18,2)");
            e.Property(x => x.DiscountAmount).HasColumnType("decimal(18,2)");
            e.Property(x => x.TotalAmount).HasColumnType("decimal(18,2)");
            e.HasIndex(x => x.BillNo).IsUnique().HasFilter("[IsDeleted] = 0");
            e.HasIndex(x => x.CartId).IsUnique().HasFilter("[IsDeleted] = 0");
            e.HasOne(x => x.Cart).WithOne(x => x.Bill)
                .HasForeignKey<Bill>(x => x.CartId);
            e.HasOne(x => x.Cashier).WithMany()
                .HasForeignKey(x => x.CashierId).OnDelete(DeleteBehavior.Restrict);
        });

        // ── BillDetail ───────────────────────────────────────────────────────
        builder.Entity<BillDetail>(e =>
        {
            e.Property(x => x.ProductName).HasMaxLength(150).IsRequired();
            e.Property(x => x.OptionsText).HasMaxLength(255);
            e.Property(x => x.UnitPrice).HasColumnType("decimal(18,2)");
            e.Property(x => x.LineTotal).HasColumnType("decimal(18,2)");
            e.HasIndex(x => x.CartItemId).IsUnique().HasFilter("[IsDeleted] = 0");
            // Tránh cascade cycle: Bill→Cart→CartItems và BillDetails→CartItems
            e.HasOne(x => x.CartItem).WithOne(x => x.BillDetail)
                .HasForeignKey<BillDetail>(x => x.CartItemId)
                .OnDelete(DeleteBehavior.NoAction);
            e.HasOne(x => x.Bill).WithMany(x => x.BillDetails)
                .HasForeignKey(x => x.BillId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // ── PaymentMethod ────────────────────────────────────────────────────
        builder.Entity<PaymentMethod>(e =>
        {
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Code).HasMaxLength(30).IsRequired();
            e.Property(x => x.Name).HasMaxLength(50).IsRequired();
        });

        // ── Payment ──────────────────────────────────────────────────────────
        builder.Entity<Payment>(e =>
        {
            e.Property(x => x.Amount).HasColumnType("decimal(18,2)");
            e.Property(x => x.ReferenceNo).HasMaxLength(100);
            e.HasOne(x => x.Cashier).WithMany(x => x.Payments)
                .HasForeignKey(x => x.CashierId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Bill).WithMany(x => x.Payments)
                .HasForeignKey(x => x.BillId).OnDelete(DeleteBehavior.NoAction);
        });

        // ── AuditLog ─────────────────────────────────────────────────────────
        builder.Entity<AuditLog>(e =>
        {
            e.Property(x => x.Action).HasMaxLength(50).IsRequired();
            e.Property(x => x.EntityName).HasMaxLength(50).IsRequired();
            e.Property(x => x.EntityId).HasMaxLength(50).IsRequired();
            e.Property(x => x.IpAddress).HasMaxLength(45);
            e.HasOne(x => x.User).WithMany()
                .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.SetNull);
        });

        // ── SystemSetting ────────────────────────────────────────────────────
        builder.Entity<SystemSetting>(e =>
        {
            e.Property(x => x.SettingKey).HasMaxLength(50).IsRequired();
            e.Property(x => x.SettingValue).HasMaxLength(255).IsRequired();
            e.Property(x => x.Description).HasMaxLength(255);
            e.HasIndex(x => x.SettingKey).IsUnique().HasFilter("[IsDeleted] = 0");
        });
    }
}
