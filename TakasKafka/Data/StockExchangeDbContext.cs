using Microsoft.EntityFrameworkCore;
using TakasKafka.Models;

namespace TakasKafka.Data;

public class StockExchangeDbContext: DbContext
{
    public StockExchangeDbContext(DbContextOptions<StockExchangeDbContext> options) : base(options)
    {
    }
    public DbSet<Security> Securities { get; set; }
    public DbSet<Order> Orders { get; set; }
    public DbSet<Trade> Trades { get; set; }
    public DbSet<MarketData> MarketData { get; set; }
    public DbSet<BinanceTrade> BinanceTrades { get; set; }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Security>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Symbol).IsRequired().HasMaxLength(10);
            entity.Property(e => e.CompanyName).IsRequired().HasMaxLength(255);
            entity.Property(e => e.CurrentPrice).HasPrecision(18, 2);
            entity.Property(e => e.PreviousPrice).HasPrecision(18, 2);
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ClientId).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Price).HasPrecision(18, 2);
            entity.Property(e => e.AvgExecutionPrice).HasPrecision(18, 2);

            entity.HasOne(e => e.Security)
                  .WithMany(s => s.Orders)
                  .HasForeignKey(e => e.SecurityId);
        });

        modelBuilder.Entity<Trade>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Price).HasPrecision(18, 2);

            entity.HasOne(e => e.Security)
                  .WithMany(s => s.Trades)
                  .HasForeignKey(e => e.SecurityId);

            entity.HasOne(e => e.BuyOrder)
                  .WithMany()
                  .HasForeignKey(e => e.BuyOrderId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.SellOrder)
                  .WithMany()
                  .HasForeignKey(e => e.SellOrderId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MarketData>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Price).HasPrecision(18, 2);
            entity.Property(e => e.Timestamp).IsRequired();
            entity.Property(e => e.Type).IsRequired();

            entity.HasOne(e => e.Security)
                  .WithMany()
                  .HasForeignKey(e => e.SecurityId);

            entity.HasIndex(e => new { e.SecurityId, e.Timestamp });
        });

        modelBuilder.Entity<BinanceTrade>(entity =>
        {
            entity.HasKey(e => e.TradeId);
            entity.Property(e => e.Price).HasPrecision(18, 2);
            entity.Property(e => e.EventTime);
            entity.Property(e => e.Quantity);
            entity.Property(e => e.TradingSymbol);
            entity.Property(e => e.EventType);
            entity.Property(e => e.CreatedAt);
            entity.Property(e => e.TradeCompletedTime);
        });
    }
}
