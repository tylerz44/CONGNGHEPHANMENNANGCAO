
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TechMarket.Models;

// Alias tránh trùng tên namespace Store
using StoreEntity = TechMarket.Models.Store;

namespace TechMarket.Data
{
    public class ApplicationDbContext
        : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // Quản lý cửa hàng
        public DbSet<StoreEntity> Stores { get; set; }

        // Yêu cầu đăng ký mở cửa hàng
        public DbSet<StoreRegistrationRequest>
            StoreRegistrationRequests
        { get; set; }

        // Quản lý danh mục
        public DbSet<Category> Categories { get; set; }

        // Quản lý sản phẩm
        public DbSet<Product> Products { get; set; }

        // Quản lý biến thể sản phẩm
        public DbSet<ProductVariant> ProductVariants { get; set; }

        // Quản lý giỏ hàng
        public DbSet<Cart> Carts { get; set; }

        public DbSet<CartItem> CartItems { get; set; }
        // Quản lý đơn hàng
        public DbSet<Order> Orders { get; set; }

        public DbSet<StoreOrder> StoreOrders { get; set; }
        // Chi tiết đơn hàng
        public DbSet<OrderDetail> OrderDetails { get; set; }
        protected override void OnModelCreating(
            ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<StoreOrder>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.ToTable("StoreOrder");
                entity.HasOne(x => x.Order)
                    .WithMany()
                    .HasForeignKey(x => x.OrderId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(x => x.Store)
                    .WithMany()
                    .HasForeignKey(x => x.StoreId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.Property(x => x.Status)
                    .IsRequired()
                    .HasMaxLength(50);

                entity.HasIndex(x => new { x.OrderId, x.StoreId })
                    .IsUnique();
            });
            // =====================================
            // CẤU HÌNH CART ITEM
            // =====================================

            // Một giỏ hàng có nhiều CartItems
            modelBuilder.Entity<CartItem>()
                .HasOne(ci => ci.Cart)
                .WithMany(c => c.CartItems)
                .HasForeignKey(ci => ci.CartId)
                .OnDelete(DeleteBehavior.Cascade);

            // Một sản phẩm có thể xuất hiện trong nhiều CartItems
            modelBuilder.Entity<CartItem>()
                .HasOne(ci => ci.Product)
                .WithMany()
                .HasForeignKey(ci => ci.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            // Một biến thể có thể xuất hiện trong nhiều giỏ hàng
            modelBuilder.Entity<CartItem>()
                .HasOne(ci => ci.ProductVariant)
                .WithMany()
                .HasForeignKey(ci => ci.ProductVariantId)
                .OnDelete(DeleteBehavior.Restrict);

            // Trong cùng một giỏ hàng,
            // một biến thể chỉ được xuất hiện một lần
            modelBuilder.Entity<CartItem>()
                .HasIndex(ci => new
                {
                    ci.CartId,
                    ci.ProductVariantId
                })
                .IsUnique();

            // =====================================
            // CẤU HÌNH ORDER
            // =====================================

            // Một khách hàng có nhiều đơn hàng
            modelBuilder.Entity<Order>()
                .HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(o => o.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Định dạng tiền
            modelBuilder.Entity<Order>()
                .Property(o => o.TotalAmount)
                .HasPrecision(18, 2);

            // =====================================
            // CẤU HÌNH ORDER DETAIL
            // =====================================

            // Một đơn hàng có nhiều chi tiết
            modelBuilder.Entity<OrderDetail>()
                .HasOne(od => od.Order)
                .WithMany(o => o.OrderDetails)
                .HasForeignKey(od => od.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            // Liên kết sản phẩm
            modelBuilder.Entity<OrderDetail>()
                .HasOne(od => od.Product)
                .WithMany()
                .HasForeignKey(od => od.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            // Liên kết biến thể
            modelBuilder.Entity<OrderDetail>()
                .HasOne(od => od.ProductVariant)
                .WithMany()
                .HasForeignKey(od => od.ProductVariantId)
                .OnDelete(DeleteBehavior.Restrict);

            // Liên kết cửa hàng
            modelBuilder.Entity<OrderDetail>()
                .HasOne(od => od.Store)
                .WithMany()
                .HasForeignKey(od => od.StoreId)
                .OnDelete(DeleteBehavior.Restrict);

            // Định dạng tiền
            modelBuilder.Entity<OrderDetail>()
                .Property(od => od.UnitPrice)
                .HasPrecision(18, 2);

            modelBuilder.Entity<OrderDetail>()
                .Property(od => od.TotalPrice)
                .HasPrecision(18, 2);


        }
    }
}
