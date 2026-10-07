using Microsoft.EntityFrameworkCore;
using SyteQuery.Features.Database.Entities;

namespace SyteQuery.Features.Database.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    // The single local user (see LocalUser) - no longer an ASP.NET Identity table.
    // Data Protection keys are no longer persisted here either; a single-machine
    // desktop app uses the framework's default file-system-backed key store instead.
    public DbSet<ApplicationUser> Users { get; set; }

    public DbSet<UserProfile> UserProfiles { get; set; }
    public DbSet<UserEnvironmentDb> UserEnvironments { get; set; }
    public DbSet<QuerySnippetDb> QuerySnippets { get; set; }
    public DbSet<QueryHistoryDb> QueryHistory { get; set; }
    public DbSet<AuditLog> AuditLogs { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // ApplicationUser configuration (the single local user - see LocalUser)
        builder.Entity<ApplicationUser>(entity =>
        {
            entity.HasKey(e => e.Id);
        });

        // UserProfile configuration
        builder.Entity<UserProfile>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne<ApplicationUser>()
                  .WithOne()
                  .HasForeignKey<UserProfile>(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => e.UserId).IsUnique();
            entity.Property(e => e.IsActive).HasDefaultValue(true);
        });

        // UserEnvironmentDb configuration
        builder.Entity<UserEnvironmentDb>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne<ApplicationUser>()
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.UserId, e.EnvironmentId }).IsUnique();
            entity.Property(e => e.IdoName).HasMaxLength(100);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
        });

        // QuerySnippetDb configuration
        builder.Entity<QuerySnippetDb>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne<ApplicationUser>()
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.UserId, e.SnippetId }).IsUnique();
            entity.Property(e => e.IsActive).HasDefaultValue(true);
        });

        // QueryHistoryDb configuration
        builder.Entity<QueryHistoryDb>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne<ApplicationUser>()
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.UserId, e.ExecutedAt });
            entity.HasIndex(e => new { e.UserId, e.HistoryId }).IsUnique();
        });

        // AuditLog configuration
        builder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.UserId, e.CreatedAt });
            entity.HasIndex(e => new { e.Action, e.CreatedAt });
        });

    }
}

