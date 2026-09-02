using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using LinkAudioPlayer.Domain;

namespace LinkAudioPlayer.Data;

/// <summary>SQLite context holding the library and playlists.</summary>
public sealed class LibraryDbContext : DbContext
{
    /// <summary>Creates the context with the supplied options.</summary>
    public LibraryDbContext(DbContextOptions<LibraryDbContext> options)
        : base(options)
    {
    }

    /// <summary>Tracks in the library.</summary>
    public DbSet<Track> Tracks => Set<Track>();

    /// <summary>User created playlists.</summary>
    public DbSet<Playlist> Playlists => Set<Playlist>();

    /// <summary>Ordered playlist membership rows.</summary>
    public DbSet<PlaylistItem> PlaylistItems => Set<PlaylistItem>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Track>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.Property(t => t.SourceUrl).IsRequired();
            entity.Property(t => t.Provider).IsRequired();
            entity.Property(t => t.Title).IsRequired();
            entity.Property(t => t.CacheKey).IsRequired();
            entity.Property(t => t.AddedAt).HasConversion(UtcMilliseconds);
            entity.HasIndex(t => t.Position);
            entity.HasIndex(t => t.SourceUrl).IsUnique();
            entity.HasIndex(t => t.CacheKey).IsUnique();
        });

        modelBuilder.Entity<Playlist>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Name).IsRequired();
            entity.Property(p => p.CreatedAt).HasConversion(UtcMilliseconds);
            entity.HasIndex(p => p.Position);
            entity.Property(p => p.UpdatedAt).HasConversion(UtcMilliseconds);
        });

        modelBuilder.Entity<PlaylistItem>(entity =>
        {
            entity.HasKey(i => i.Id);

            entity.HasOne(i => i.Playlist)
                .WithMany(p => p.Items)
                .HasForeignKey(i => i.PlaylistId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(i => i.Track)
                .WithMany(t => t.PlaylistItems)
                .HasForeignKey(i => i.TrackId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(i => new { i.PlaylistId, i.Position });
        });
    }

    /// <summary>
    /// Stores timestamps as unix milliseconds.
    /// SQLite cannot ORDER BY a DateTimeOffset column, and a plain integer sorts correctly.
    /// </summary>
    private static readonly ValueConverter<DateTimeOffset, long> UtcMilliseconds = new(
        value => value.ToUnixTimeMilliseconds(),
        value => DateTimeOffset.FromUnixTimeMilliseconds(value));
}
