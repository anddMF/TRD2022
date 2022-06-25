using Binance.Net.Enums;
using Binance.Net.Interfaces;
using Binance.Net.Objects.Spot.MarketData;
using Binance.Net.Objects.Spot.SpotData;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Trade02.Infra.DAL.Interfaces
{
    public interface IAPICommunication
    {
        public Task<List<IBinanceTick>> GetTickers();
        public Task<IBinanceTick> GetTicker(string symbol);
        public Task<BinanceAveragePrice> GetAvgPrice(string symbol);
        public Task<BinanceOrderBook> GetOrderBook(string symbol, int limit);
        public Task<List<IBinanceKline>> GetKlines(string symbol, KlineInterval interval);
        public Task<BinancePlacedOrder> PlaceOrder(string symbol, decimal quantity, OrderSide operation);
        public Task<BinanceAccountInfo> GetAccountInfo();

        public Task<List<T>> GetStuff<T>(string site, string endpoint);
        public string CreateUrlQuery(string endpoint, Dictionary<string, string> param);
    }
}
