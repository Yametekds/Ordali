using Microsoft.EntityFrameworkCore;

namespace Ordali.Api.Data;

public sealed class OrdaliDbContext(DbContextOptions<OrdaliDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<ProfileTemplate> ProfileTemplates => Set<ProfileTemplate>();

    public DbSet<Site> Sites => Set<Site>();

    public DbSet<Location> Locations => Set<Location>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<StockLine> StockLines => Set<StockLine>();

    public DbSet<StockMovement> StockMovements => Set<StockMovement>();

    public DbSet<Receipt> Receipts => Set<Receipt>();

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<StockMovement>())
        {
            if (entry.State is EntityState.Modified or EntityState.Deleted)
            {
                throw new InvalidOperationException("The movement log is append-only; record an adjustment instead.");
            }
        }

        foreach (var entry in ChangeTracker.Entries<StockLine>())
        {
            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                entry.Entity.Version = Guid.NewGuid();
            }
        }

        foreach (var entry in ChangeTracker.Entries<ReceiptLine>())
        {
            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                entry.Entity.Version = Guid.NewGuid();
            }
        }

        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // One schema per module, so a module can later move to its own database.
        modelBuilder.Entity<User>(user =>
        {
            user.ToTable("Users", "identity");
            user.Property(u => u.Name).HasMaxLength(120);
            user.Property(u => u.Email).HasMaxLength(256);
            user.HasIndex(u => u.Email).IsUnique();
            user.Property(u => u.Category).HasConversion<string>().HasMaxLength(16);
            user.HasOne<ProfileTemplate>().WithMany().HasForeignKey(u => u.TemplateId).OnDelete(DeleteBehavior.SetNull);
            user.HasIndex(u => u.Category);
        });

        modelBuilder.Entity<ProfileTemplate>(template =>
        {
            template.ToTable("ProfileTemplates", "identity");
            template.Property(t => t.Name).HasMaxLength(60);
            template.Property(t => t.Description).HasMaxLength(200);
            template.Property(t => t.Category).HasConversion<string>().HasMaxLength(16);
            template.HasIndex(t => t.Name).IsUnique();
        });

        modelBuilder.Entity<Site>(site =>
        {
            site.ToTable("Sites", "locations");
            site.Property(s => s.Code).HasMaxLength(16);
            site.Property(s => s.Name).HasMaxLength(120);
            site.HasIndex(s => s.Code).IsUnique();
        });

        modelBuilder.Entity<Location>(location =>
        {
            location.ToTable("Locations", "locations");
            location.Property(l => l.Code).HasMaxLength(32);
            location.Property(l => l.Zone).HasMaxLength(60);
            location.Property(l => l.Type).HasConversion<string>().HasMaxLength(16);
            location.HasOne<Site>().WithMany().HasForeignKey(l => l.SiteId);
            // Not unique: a second location with the same code is allowed after a warning.
            location.HasIndex(l => new { l.SiteId, l.Code });
        });

        modelBuilder.Entity<Product>(product =>
        {
            product.ToTable("Products", "catalog");
            product.Property(p => p.Sku).HasMaxLength(32);
            product.Property(p => p.Name).HasMaxLength(120);
            product.Property(p => p.Gtin).HasMaxLength(14);
            product.Property(p => p.Unit).HasMaxLength(16);
            product.Property(p => p.MinStock).HasPrecision(18, 3);
            product.HasIndex(p => p.Sku).IsUnique();
            product.HasIndex(p => p.Gtin);
            product.HasOne<Location>().WithMany().HasForeignKey(p => p.DefaultLocationId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<StockLine>(line =>
        {
            line.ToTable("StockLines", "inventory");
            line.Property(s => s.Lot).HasMaxLength(40);
            line.Property(s => s.Quantity).HasPrecision(18, 3);
            line.Property(s => s.Version).IsConcurrencyToken();
            line.HasOne<Product>().WithMany().HasForeignKey(s => s.ProductId);
            line.HasOne<Location>().WithMany().HasForeignKey(s => s.LocationId);
            // No filter: lines without a lot must be unique as well.
            line.HasIndex(s => new { s.ProductId, s.LocationId, s.Lot }).IsUnique().HasFilter(null);
        });

        modelBuilder.Entity<StockMovement>(movement =>
        {
            movement.ToTable("StockMovements", "inventory");
            movement.Property(m => m.Type).HasConversion<string>().HasMaxLength(16);
            movement.Property(m => m.Lot).HasMaxLength(40);
            movement.Property(m => m.Quantity).HasPrecision(18, 3);
            movement.Property(m => m.Reason).HasMaxLength(200);
            movement.Property(m => m.Reference).HasMaxLength(40);
            movement.Property(m => m.UserName).HasMaxLength(120);
            movement.HasOne<Product>().WithMany().HasForeignKey(m => m.ProductId).OnDelete(DeleteBehavior.Restrict);
            movement.HasOne<Location>().WithMany().HasForeignKey(m => m.FromLocationId).OnDelete(DeleteBehavior.Restrict);
            movement.HasOne<Location>().WithMany().HasForeignKey(m => m.ToLocationId).OnDelete(DeleteBehavior.Restrict);
            movement.HasIndex(m => m.CreatedAt);
            movement.HasIndex(m => new { m.ProductId, m.CreatedAt });
        });

        modelBuilder.Entity<Receipt>(receipt =>
        {
            receipt.ToTable("Receipts", "inventory");
            receipt.Property(r => r.Code).HasMaxLength(40);
            receipt.Property(r => r.Reference).HasMaxLength(120);
            receipt.Property(r => r.UserName).HasMaxLength(120);
            receipt.HasIndex(r => r.Code).IsUnique();
            receipt.HasIndex(r => r.CompletedAt);
            receipt.HasOne<Location>().WithMany().HasForeignKey(r => r.DockLocationId).OnDelete(DeleteBehavior.Restrict);
            receipt.HasMany(r => r.Lines).WithOne().HasForeignKey(l => l.ReceiptId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ReceiptLine>(line =>
        {
            line.ToTable("ReceiptLines", "inventory");
            line.Property(l => l.Lot).HasMaxLength(40);
            line.Property(l => l.Quantity).HasPrecision(18, 3);
            line.Property(l => l.PutAwayQuantity).HasPrecision(18, 3);
            line.Property(l => l.Version).IsConcurrencyToken();
            line.HasOne<Product>().WithMany().HasForeignKey(l => l.ProductId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
