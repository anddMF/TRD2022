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
        /// <summary>
        /// Returns, in descending order, the symbols with the best valorization from the last X period
        /// </summary>
        /// <param name="numberOfSymbols">amount of symbols to be returned</param>
        /// <param name="currencySymbol">symbol from the asset that will be use to buy</param>
        /// <param name="maxPercentage">max percentage from the price variation</param>
        /// <returns></returns>
        public Task<List<IBinanceTick>> GetTopPercentages(int numberOfSymbols, string currencySymbol, decimal maxPercentage, List<string> ownedSymbols);

        /// <summary>
        /// Returns the data from the last 24h of a specific symbol.
        /// </summary>
        /// <param name="symbol"></param>
        /// <returns></returns>
        public Task<IBinanceTick> GetSingleTicker(string symbol);

        /// <summary>
        /// Returns the recent data from the list of symbols on 'toMonitor'.
        /// </summary>
        /// <param name="toMonitor">list of coins to be monitored</param>
        /// <returns>Retorna os dados mais recentes das moedas de input</returns>
        public Task<List<IBinanceTick>> MonitorTopPercentages(List<IBinanceTick> toMonitor);

        /// <summary>
        /// Sends a buy order.
        /// </summary>
        /// <param name="symbol">symbol that will be bought</param>
        /// <returns></returns>
        public Task<BinancePlacedOrder> PlaceBuyOrder(string symbol, decimal quantity);

        /// <summary>
        /// Sends a sell order.
        /// </summary>
        /// <param name="symbol">símbolo que será vendido</param>
        /// <param name="quantity">quantidade da moeda que será vendida</param>
        /// <returns></returns>
        public Task<BinancePlacedOrder> PlaceSellOrder(string symbol, decimal quantity);

        /// <summary>
        /// Compares the list of current data from the coins against the list of previously validated, verifies if exists a valorization of X% to try to identify 
        /// an upward trend, consequently, a possible purchase opportunitie.
        /// </summary>
        /// <param name="currentData">current market data</param>
        /// <param name="previousData">previously separated data</param>
        /// <returns>list of possible opportunities</returns>
        public List<IBinanceTick> CheckOpportunities(List<IBinanceTick> currentData, List<IBinanceTick> previousData);
    }
}
