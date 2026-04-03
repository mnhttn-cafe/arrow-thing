using ArrowThing.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace ArrowThing.Server.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Score> Scores => Set<Score>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);
            entity.Property(u => u.Email).HasMaxLength(254).IsRequired();
            entity.Property(u => u.DisplayName).HasMaxLength(24).IsRequired();
            entity.Property(u => u.PasswordHash).IsRequired();
            entity.Property(u => u.CreatedAt).IsRequired();

            // Email is stored lowercase; app layer normalizes on write.
            entity.HasIndex(u => u.Email).IsUnique();
        });

        modelBuilder.Entity<Score>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Time).IsRequired();
            entity.Property(s => s.ReplayJson).IsRequired();
            entity.Property(s => s.CreatedAt).IsRequired();
            entity.Property(s => s.UpdatedAt).IsRequired();

            entity
                .HasOne(s => s.User)
                .WithMany()
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // One best score per player per board size.
            entity
                .HasIndex(s => new
                {
                    s.UserId,
                    s.BoardWidth,
                    s.BoardHeight,
                })
                .IsUnique();

            // Fast top-N leaderboard queries.
            entity.HasIndex(s => new
            {
                s.BoardWidth,
                s.BoardHeight,
                s.Time,
            });
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Timestamp).IsRequired();
            entity.Property(e => e.Event).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Email).HasMaxLength(254);
            entity.Property(e => e.IpAddress).HasMaxLength(45);
            entity.Property(e => e.Detail).HasMaxLength(500);

            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => e.Event);
            entity.HasIndex(e => e.UserId);
        });
    }
}
