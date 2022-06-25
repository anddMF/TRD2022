using Binance.Net.Interfaces;
using Binance.Net.Objects.Spot.MarketData;
using Binance.Net.Objects.Spot.SpotData;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Trade02.Models.CrossCutting;
using Trade02.Models.Trade;

namespace Trade02.Business.services
{
    public interface IPortfolioService
    {
        public Task<ManagerResponse> ManagePosition(OpportunitiesResponse opp, List<Position> positions, List<Position> toMonitor);
        public Task<BinancePlacedOrder> ExecuteSellOrder(string symbol, decimal quantity);
        public List<Position> GetLastPositions();
        public Task<Position> ValidationSellOrder(Position position, decimal currentValorization, IBinanceTick market);
        public Task<OrderResponse> ExecuteMulitpleOrder(List<string> symbols);
        public Task<Position> ExecuteSimpleOrder(string symbol, RecommendationType type);
        public Task<Position> ExecuteSimpleOrder(string symbol, RecommendationType type, decimal minPrice);
        public Task<decimal> GetUSDTAmount();
        public Task<List<BinanceBalance>> GetBalance();
        public Task<BinanceBalance> GetBalance(string symbol);
        public Task<BinanceOrderBook> GetOrderBook(string symbol, int limit);

    }
}
