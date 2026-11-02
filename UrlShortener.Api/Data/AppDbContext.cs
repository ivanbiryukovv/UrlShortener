using Microsoft.EntityFrameworkCore;
using UrlShortener.Api.Domain;

namespace UrlShortener.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Link> Links => Set<Link>();

    public DbSet<LinkVisit> Visits => Set<LinkVisit>();

    public DbSet<AuditLogEntry> AuditLog => Set<AuditLogEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Link>(entity =>
        {
            entity.ToTable("links");
            entity.HasKey(l => l.Code);
            entity.Property(l => l.Code).HasMaxLength(16);
            entity.Property(l => l.HmacSignature).HasMaxLength(64);
            entity.HasIndex(l => l.OrderId);
        });

        modelBuilder.Entity<LinkVisit>(entity =>
        {
            entity.ToTable("link_visits");
            entity.HasKey(v => v.Id);
            entity.Property(v => v.Code).HasMaxLength(16);
            entity.Property(v => v.Ip).HasMaxLength(45);
            entity.Property(v => v.UserAgent).HasMaxLength(512);
            entity.Property(v => v.Result).HasConversion<string>().HasMaxLength(32);
            entity.HasIndex(v => v.Code);
            entity.HasIndex(v => v.VisitedAt);
        });

        modelBuilder.Entity<AuditLogEntry>(entity =>
        {
            entity.ToTable("audit_log");
            entity.HasKey(a => a.Id);
            entity.Property(a => a.Action).HasConversion<string>().HasMaxLength(32);
            entity.Property(a => a.Code).HasMaxLength(16);
            entity.Property(a => a.Initiator).HasMaxLength(64);
            entity.Property(a => a.Result).HasMaxLength(256);
            entity.HasIndex(a => a.CreatedAt);
        });
    }
}
