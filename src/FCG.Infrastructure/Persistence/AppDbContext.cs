using FCG.Domain.Games;
using FCG.Domain.Library;
using FCG.Domain.Promotions;
using FCG.Domain.Purchases;
using Microsoft.EntityFrameworkCore;

namespace FCG.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Game> Games => Set<Game>();
    public DbSet<AcquiredGame> AcquiredGames => Set<AcquiredGame>();
    public DbSet<Promotion> Promotions => Set<Promotion>();
    public DbSet<PendingPurchase> PendingPurchases => Set<PendingPurchase>();
    public DbSet<OrderOutboxMessage> OrderOutbox => Set<OrderOutboxMessage>();
    public DbSet<ProcessedPaymentEvent> ProcessedPaymentEvents => Set<ProcessedPaymentEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OrderOutboxMessage>(builder =>
        {
            builder.ToTable("OrderOutbox");
            builder.HasKey(x => x.EventId);
            builder.Property(x => x.OrderId).IsRequired();
            builder.Property(x => x.Payload).IsRequired();
            builder.HasIndex(x => x.OrderId).IsUnique();
            builder.HasIndex(x => new { x.PublishedAtUtc, x.CreatedAtUtc });
        });
        modelBuilder.Entity<ProcessedPaymentEvent>(builder =>
        {
            builder.ToTable("ProcessedPaymentEvents");
            builder.HasKey(x => x.EventId);
            builder.Property(x => x.OrderId).IsRequired();
            builder.Property(x => x.ProcessedAtUtc).IsRequired();
            builder.HasIndex(x => x.OrderId).IsUnique();
        });
        modelBuilder.Entity<PendingPurchase>(builder =>
        {
            builder.ToTable("PendingPurchases");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.UserId).IsRequired();
            builder.Property(x => x.GameId).IsRequired();
            builder.Property(x => x.Price).HasPrecision(18, 2);
            builder.Property(x => x.RecipientEmail).IsRequired().HasMaxLength(320);
            builder.Property(x => x.CreatedAtUtc).IsRequired();
            builder.HasIndex(x => new { x.UserId, x.GameId }).IsUnique();
            builder.HasOne<Game>().WithMany().HasForeignKey(x => x.GameId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<Game>(builder =>
        {
            builder.ToTable("Games");
            builder.HasKey(game => game.Id);
            builder.Property(game => game.Title).IsRequired().HasMaxLength(200);
            builder.Property(game => game.Description).IsRequired().HasMaxLength(2000);
            builder.Property(game => game.Genre).IsRequired().HasMaxLength(100);
            builder.Property(game => game.Price).HasPrecision(18, 2);
            builder.Property(game => game.CreatedByUserId).IsRequired();
            builder.Property(game => game.CreatedAtUtc).IsRequired();
        });

        modelBuilder.Entity<AcquiredGame>(builder =>
        {
            builder.ToTable("AcquiredGames");
            builder.HasKey(acquisition => acquisition.Id);
            builder.Property(acquisition => acquisition.UserId).IsRequired();
            builder.Property(acquisition => acquisition.GameId).IsRequired();
            builder.Property(acquisition => acquisition.AcquiredAtUtc).IsRequired();
            builder.HasIndex(acquisition => new { acquisition.UserId, acquisition.GameId }).IsUnique();
            builder.HasOne(acquisition => acquisition.Game)
                .WithMany()
                .HasForeignKey(acquisition => acquisition.GameId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Promotion>(builder =>
        {
            builder.ToTable("Promotions");
            builder.HasKey(promotion => promotion.Id);
            builder.Property(promotion => promotion.Name).IsRequired().HasMaxLength(200);
            builder.Property(promotion => promotion.Code).IsRequired().HasMaxLength(50);
            builder.Property(promotion => promotion.NormalizedCode).IsRequired().HasMaxLength(50);
            builder.HasIndex(promotion => promotion.NormalizedCode).IsUnique();
            builder.Property(promotion => promotion.DiscountPercentage).IsRequired().HasPrecision(5, 2);
            builder.Property(promotion => promotion.StartsAtUtc).IsRequired();
            builder.Property(promotion => promotion.EndsAtUtc).IsRequired();
            builder.Property(promotion => promotion.CreatedByUserId).IsRequired();
            builder.Property(promotion => promotion.CreatedAtUtc).IsRequired();
        });
    }
}
