using Binance.Net.Interfaces;
using Binance.Net.Objects.Spot.SpotData;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Trade02.Business.services.Interfaces
{
    public interface IMarketService
    {
        public Task<List<IBinanceTick>> GetTopPercentages(int numberOfSymbols, string currencySymbol, decimal maxPercentage, List<string> ownedSymbols);
        public Task<IBinanceTick> GetSingleTicker(string symbol);
        public Task<List<IBinanceTick>> MonitorTopPercentages(List<IBinanceTick> toMonitor);
        public Task<BinancePlacedOrder> PlaceBuyOrder(string symbol, decimal quantity);
        public Task<BinancePlacedOrder> PlaceSellOrder(string symbol, decimal quantity);
        public List<IBinanceTick> CheckOpportunities(List<IBinanceTick> currentData, List<IBinanceTick> previousData);
    }
}
