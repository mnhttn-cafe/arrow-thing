using ArrowThing.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace ArrowThing.Server.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options) { }

    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);
            entity.Property(u => u.Username).HasMaxLength(20).IsRequired();
            entity.Property(u => u.DisplayName).HasMaxLength(24).IsRequired();
            entity.Property(u => u.PasswordHash).IsRequired();
            entity.Property(u => u.CreatedAt).IsRequired();

            // Username is stored lowercase; app layer normalizes on write.
            entity.HasIndex(u => u.Username).IsUnique();
        });
    }
}
