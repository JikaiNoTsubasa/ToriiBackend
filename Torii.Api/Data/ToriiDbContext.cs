using Microsoft.EntityFrameworkCore;
using Torii.Api.Models;

namespace Torii.Api.Data;

/// <summary>
/// Contexte EF Core de Torii. Ajoutez ici les DbSet du modèle de données métier.
/// </summary>
public class ToriiDbContext(DbContextOptions<ToriiDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(u => u.Email).IsUnique();
            entity.Property(u => u.Email).HasMaxLength(256);
            entity.Property(u => u.DisplayName).HasMaxLength(128);
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasIndex(t => t.Token).IsUnique();
            entity.HasOne(t => t.User)
                  .WithMany(u => u.RefreshTokens)
                  .HasForeignKey(t => t.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Toujours en dernier : couvre les enums de toutes les entités déclarées ci-dessus.
        modelBuilder.UseUpperCaseEnumStrings();
    }
}
