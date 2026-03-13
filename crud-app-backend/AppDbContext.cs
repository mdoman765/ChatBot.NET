using crud_app_backend.Models;
using Microsoft.EntityFrameworkCore;

namespace crud_app_backend
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        // ── existing tables ──────────────────────────────────────────────
        public DbSet<Category> Categories { get; set; } = null!;
        public DbSet<SubCategory> SubCategories { get; set; } = null!;
        public DbSet<Product> Products { get; set; } = null!;
        public DbSet<Order> Orders { get; set; } = null!;
        public DbSet<Feedback> Feedbacks { get; set; } = null!;
        public DbSet<User> Users { get; set; } = null!;

        // ── WhatsApp tables ──────────────────────────────────────────────
        public DbSet<WhatsAppSession> WhatsAppSessions { get; set; } = null!;
        public DbSet<WhatsAppSessionHistory> WhatsAppSessionHistories { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // ── existing config (unchanged) ──────────────────────────────
            modelBuilder.Entity<Product>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Price).HasColumnType("decimal(18,2)");
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            });

            modelBuilder.Entity<Category>();
            modelBuilder.Entity<SubCategory>();
            modelBuilder.Entity<Product>();

            modelBuilder.Entity<Product>()
                .HasOne(p => p.SubCategory)
                .WithMany(sc => sc.Products)
                .HasForeignKey(p => p.SubcategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<SubCategory>()
                .HasOne(sc => sc.Category)
                .WithMany(c => c.SubCategories)
                .HasForeignKey(sc => sc.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            // ── WhatsAppSessions ─────────────────────────────────────────
            modelBuilder.Entity<WhatsAppSession>(entity =>
            {
                entity.ToTable("WhatsAppSessions", "dbo");

                entity.HasKey(e => e.Phone);

                entity.Property(e => e.Phone)
                      .HasMaxLength(30)
                      .IsRequired();

                entity.Property(e => e.CurrentStep)
                      .HasMaxLength(50)
                      .IsRequired()
                      .HasDefaultValue("INIT");

                entity.Property(e => e.PreviousStep)
                      .HasMaxLength(50)
                      .IsRequired()
                      .HasDefaultValue("INIT");

                entity.Property(e => e.TempData)
                      .HasColumnType("nvarchar(max)")
                      .IsRequired()
                      .HasDefaultValue("{}");

                entity.Property(e => e.PendingReport)
                      .IsRequired()
                      .HasDefaultValue(false);

                entity.Property(e => e.PendingShopReg)
                      .IsRequired()
                      .HasDefaultValue(false);

                entity.Property(e => e.CreatedAt)
                      .IsRequired()
                      .HasDefaultValueSql("SYSUTCDATETIME()");

                entity.Property(e => e.UpdatedAt)
                      .IsRequired()
                      .HasDefaultValueSql("SYSUTCDATETIME()");

                entity.HasMany(e => e.History)
                      .WithOne(h => h.Session)
                      .HasForeignKey(h => h.Phone)
                      .HasConstraintName("FK_SessionHistory_Phone")
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // ── WhatsAppSessionHistory ───────────────────────────────────
            modelBuilder.Entity<WhatsAppSessionHistory>(entity =>
            {
                entity.ToTable("WhatsAppSessionHistory", "dbo");

                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id)
                      .UseIdentityColumn();

                entity.Property(e => e.Phone)
                      .HasMaxLength(30)
                      .IsRequired();

                entity.Property(e => e.FromStep)
                      .HasMaxLength(50)
                      .IsRequired();

                entity.Property(e => e.ToStep)
                      .HasMaxLength(50)
                      .IsRequired();

                entity.Property(e => e.RawMessage)
                      .HasMaxLength(1000);

                entity.Property(e => e.TempDataSnapshot)
                      .HasColumnType("nvarchar(max)");

                entity.Property(e => e.CreatedAt)
                      .IsRequired()
                      .HasDefaultValueSql("SYSUTCDATETIME()");

                entity.HasIndex(e => new { e.Phone, e.CreatedAt })
                      .HasDatabaseName("IX_WhatsAppSessionHistory_Phone_CreatedAt");
            });
        }
    }
}
