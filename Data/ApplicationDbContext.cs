using DirtyCoins.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;

namespace DirtyCoins.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        // DbSets
        public DbSet<User> Users { get; set; } = null!;
        public DbSet<Customer> Customers { get; set; } = null!;
        public DbSet<Employee> Employee { get; set; } = null!;
        public DbSet<Store> Stores { get; set; } = null!;
        public DbSet<Category> Categories { get; set; } = null!;
        public DbSet<Product> Products { get; set; } = null!;
        public DbSet<Inventory> Inventory { get; set; } = null!;
        public DbSet<Order> Orders { get; set; } = null!;
        public DbSet<OrderDetail> OrderDetail { get; set; } = null!;
        public DbSet<Feedback> Feedbacks { get; set; } = null!;
        public DbSet<Report> Reports { get; set; } = null!;
        public DbSet<SystemLog> SystemLogs { get; set; } = null!;
        public DbSet<Promotion> Promotions { get; set; } = null!;
        public DbSet<PromotionProduct> PromotionProducts { get; set; } = null!;
        public DbSet<Contact> Contacts { get; set; }
        public DbSet<BulkOperation> BulkOperations { get; set; }
        public DbSet<BulkOperationItem> BulkOperationItems { get; set; }
        public DbSet<CustomerRankStat> CustomerRankStats { get; set; }
        public DbSet<FeedbackReply> FeedbackReplies { get; set; }
        public DbSet<FeedbackLike> FeedbackLikes { get; set; }
        public DbSet<OperationCost> OperationCosts { get; set; }
        public DbSet<Imported> Importeds { get; set; }
        public DbSet<MonthlyInventory> MonthlyInventorys { get; set; }
        public DbSet<Director> Directors { get; set; }
        public DbSet<MaintenanceLog> MaintenanceLogs { get; set; }
        public DbSet<ProductImage> ProductImages { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // -----------------------------
            // Primary Keys
            // -----------------------------
            modelBuilder.Entity<User>().HasKey(u => u.IdUser);
            modelBuilder.Entity<Customer>().HasKey(c => c.IdCustomer);
            modelBuilder.Entity<Employee>().HasKey(e => e.IdEmployee);
            modelBuilder.Entity<Store>().HasKey(s => s.IdStore);
            modelBuilder.Entity<Category>().HasKey(c => c.IdCategory);
            modelBuilder.Entity<Product>().HasKey(p => p.IdProduct);
            modelBuilder.Entity<Inventory>().HasKey(i => i.IdInventory);
            modelBuilder.Entity<Order>().HasKey(o => o.IdOrder);
            modelBuilder.Entity<OrderDetail>().HasKey(od => od.IdDetail);
            modelBuilder.Entity<Feedback>().HasKey(f => f.IdFeedback);
            modelBuilder.Entity<Report>().HasKey(r => r.IdReport);
            modelBuilder.Entity<SystemLog>().HasKey(s => s.IdLog);
            modelBuilder.Entity<PromotionProduct>().ToTable("PromotionProduct").HasKey(pp => pp.IdPromotionProduct);
            modelBuilder.Entity<Promotion>().ToTable("Promotion").HasKey(pp => pp.IdPromotion);
            modelBuilder.Entity<Contact>().ToTable("Contacts").HasKey(ct => ct.IdContact);

            // -----------------------------
            // Unique Index
            // -----------------------------
            modelBuilder.Entity<User>().HasIndex(u => u.Email).IsUnique();
            modelBuilder.Entity<Inventory>().HasIndex(i => new { i.IdStore, i.IdProduct }).IsUnique();

            // -----------------------------
            // Relationships
            // -----------------------------

            // Customer -> User (1-1)
            modelBuilder.Entity<Customer>()
                .HasOne(c => c.User)
                .WithMany()
                .HasForeignKey(c => c.IdUser)
                .OnDelete(DeleteBehavior.Cascade);

            // Customer -> Orders (1-N)
            modelBuilder.Entity<Customer>()
                .HasMany(c => c.Orders)
                .WithOne(o => o.Customer)
                .HasForeignKey(o => o.IdCustomer)
                .OnDelete(DeleteBehavior.Cascade);

            // Customer -> Feedbacks (1-N)
            modelBuilder.Entity<Customer>()
                .HasMany(c => c.Feedbacks)
                .WithOne(f => f.Customer)
                .HasForeignKey(f => f.IdCustomer)
                .OnDelete(DeleteBehavior.Cascade);

            // OrderDetail -> Order (N-1)
            modelBuilder.Entity<OrderDetail>()
                .HasOne(od => od.Order)
                .WithMany(o => o.OrderDetails)
                .HasForeignKey(od => od.IdOrder);

            // OrderDetail -> Product (N-1)
            modelBuilder.Entity<OrderDetail>()
                .HasOne(od => od.Product)
                .WithMany(p => p.OrderDetails)
                .HasForeignKey(od => od.IdProduct);

            // Inventory -> Store & Product
            modelBuilder.Entity<Inventory>()
                .HasOne(i => i.Store)
                .WithMany(s => s.Inventories)
                .HasForeignKey(i => i.IdStore);

            modelBuilder.Entity<Inventory>()
                .HasOne(i => i.Product)
                .WithMany()
                .HasForeignKey(i => i.IdProduct);

            modelBuilder.Entity<Product>()
                .HasOne(p => p.Category)
                .WithMany(c => c.Products) // nếu Category có collection
                .HasForeignKey(p => p.IdCategory) // sử dụng đúng tên cột trong DB
                .OnDelete(DeleteBehavior.SetNull);
            modelBuilder.Entity<Customer>()
                .HasOne(c => c.User)
                .WithOne()
                .HasForeignKey<Customer>(c => c.IdUser)
                .OnDelete(DeleteBehavior.Cascade);

            // Promotion - Product (N-N)
            modelBuilder.Entity<PromotionProduct>()
                .HasIndex(pp => new { pp.IdPromotion, pp.IdProduct })
                .IsUnique();

            modelBuilder.Entity<PromotionProduct>()
                .HasOne(pp => pp.Promotion)
                .WithMany(p => p.PromotionProducts)
                .HasForeignKey(pp => pp.IdPromotion)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PromotionProduct>()
                .HasOne(pp => pp.Product)
                .WithMany()
                .HasForeignKey(pp => pp.IdProduct)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<Product>()
                .HasOne(p => p.Store)
                .WithMany(s => s.Products)
                .HasForeignKey(p => p.IdStore)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
