using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using TechnicalAnalysis.Domain.MLModels;
using TraiderAssistant.Infrastructure;
using TraiderAssistant.Infrastructure.Entities;

namespace TechnicalAnalysis.Domain.Services
{
    public class StrategyProviderService
    {
        private readonly IServiceProvider _serviceProvider;

        // Кэш в оперативной памяти: Ключ - "BNBUSDT_OneDay", Значение - распарсенные настройки
        private readonly ConcurrentDictionary<string, ActiveStrategyModel> _cache = new();

        public StrategyProviderService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        // Вызывать при старте сервера или при нажатии кнопки "Применить стратегию"
        public async Task LoadStrategiesFromDatabaseAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var activeStrategies = await dbContext.ActiveStrategies
                .Where(strategy => strategy.SelectionMethod == "WalkForward")
                .ToListAsync();

            _cache.Clear();

            foreach (var strat in activeStrategies)
            {
                var key = $"{strat.Asset}_{strat.Timeframe}";
                var weights = JsonConvert.DeserializeObject<WeightsJsonSchema>(strat.IndicatorWeightsJson);

                _cache[key] = new ActiveStrategyModel
                {
                    BuyThreshold = strat.BuyThreshold,
                    SellThreshold = strat.SellThreshold,
                    Weights = weights
                };
            }

            Console.WriteLine($"[StrategyProvider] Загружено {_cache.Count} активных стратегий в кэш.");
        }

        // Мгновенное получение стратегии (О(1) сложность)
        public virtual ActiveStrategyModel GetStrategy(string asset, string timeframe)
        {
            var key = $"{asset}_{timeframe}";
            if (_cache.TryGetValue(key, out var strategy))
            {
                return strategy;
            }
            return null; // Если для пары нет стратегии, вернет null
        }
    }

    // Вспомогательная модель для кэша
    public class ActiveStrategyModel
    {
        public int BuyThreshold { get; set; }
        public int SellThreshold { get; set; }
        public WeightsJsonSchema Weights { get; set; }
    }
}
