using Microsoft.EntityFrameworkCore;
using TakasKafka.Data;
using TakasKafka.Models;
using System.Collections.Concurrent;

namespace TakasKafka.Services
{
    public class MarketDataService
    {
        private readonly StockExchangeDbContext _context;
        private readonly ILogger<MarketDataService> _logger;
        private readonly ConcurrentDictionary<string, decimal> _currentPrices = new();
        private readonly ConcurrentDictionary<string, DateTime> _lastUpdated = new();

        public MarketDataService(StockExchangeDbContext context, ILogger<MarketDataService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task UpdateMarketDataAsync(string symbol, decimal price, int volume, MarketDataType type = MarketDataType.Trade)
        {
            try
            {
                var security = await _context.Securities.FirstOrDefaultAsync(s => s.Symbol == symbol);
                if (security == null)
                {
                    _logger.LogWarning($"Security with symbol {symbol} not found");
                    return;
                }

                var marketData = new MarketData
                {
                    SecurityId = security.Id,
                    Price = price,
                    Volume = volume,
                    Timestamp = DateTime.UtcNow,
                    Type = type
                };

                _context.MarketData.Add(marketData);

                security.PreviousPrice = security.CurrentPrice;
                security.CurrentPrice = price;
                security.LastUpdated = DateTime.UtcNow;
                security.Volume += volume;

                await _context.SaveChangesAsync();

                _currentPrices[symbol] = price;
                _lastUpdated[symbol] = DateTime.UtcNow;

                _logger.LogInformation($"Updated market data for {symbol}: Price={price}, Volume={volume}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating market data for {symbol}");
            }
        }

        public decimal GetCurrentPrice(string symbol)
        {
            return _currentPrices.GetValueOrDefault(symbol, 0);
        }

        public async Task<decimal> GetCurrentPriceAsync(string symbol)
        {
            if (_currentPrices.TryGetValue(symbol, out var cachedPrice))
            {
                return cachedPrice;
            }

            var security = await _context.Securities.FirstOrDefaultAsync(s => s.Symbol == symbol);
            if (security != null)
            {
                _currentPrices[symbol] = security.CurrentPrice;
                return security.CurrentPrice;
            }

            return 0;
        }

        public async Task<List<MarketData>> GetPriceHistoryAsync(string symbol, DateTime fromDate, DateTime toDate)
        {
            var security = await _context.Securities.FirstOrDefaultAsync(s => s.Symbol == symbol);
            if (security == null) return new List<MarketData>();

            return await _context.MarketData
                .Where(md => md.SecurityId == security.Id && 
                           md.Timestamp >= fromDate && 
                           md.Timestamp <= toDate)
                .OrderBy(md => md.Timestamp)
                .ToListAsync();
        }

        public async Task<MarketData?> GetLatestMarketDataAsync(string symbol)
        {
            var security = await _context.Securities.FirstOrDefaultAsync(s => s.Symbol == symbol);
            if (security == null) return null;

            return await _context.MarketData
                .Where(md => md.SecurityId == security.Id)
                .OrderByDescending(md => md.Timestamp)
                .FirstOrDefaultAsync();
        }

        public async Task InitializePriceCacheAsync()
        {
            var securities = await _context.Securities.Where(s => s.IsActive).ToListAsync();
            foreach (var security in securities)
            {
                _currentPrices[security.Symbol] = security.CurrentPrice;
                _lastUpdated[security.Symbol] = security.LastUpdated;
            }
        }
    }
}
