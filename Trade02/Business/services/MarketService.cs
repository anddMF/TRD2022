using Binance.Net.Enums;
using Binance.Net.Interfaces;
using Binance.Net.Objects.Spot.SpotData;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Trade02.Business.services.Interfaces;
using Trade02.Infra.Cross;
using Trade02.Infra.DAL.Interfaces;
using Trade02.Models.CrossCutting;
using Trade02.Models.Trade;

namespace Trade02.Business.services
{
    /// <summary>
    /// Responsável por manipular dados de mercado.
    /// </summary>
    public class MarketService : IMarketService
    {
        private static IAPICommunication _clientSvc;
        private readonly ILogger _logger;
        private static IEventsOutput _eventsOutput;

        private readonly bool freeMode = AppSettings.TradeConfiguration.FreeMode;

        public MarketService(ILogger<MarketService> logger, IAPICommunication clientSvc, IEventsOutput eventsOutput)
        {
            _logger = logger;
            _clientSvc = clientSvc;
            _eventsOutput = eventsOutput;
        }

        /// <summary>
        /// Returns, in descending order, the symbols with the best valorization from the last X period
        /// </summary>
        /// <param name="numberOfSymbols">amount of symbols to be returned</param>
        /// <param name="currencySymbol">symbol from the asset that will be use to buy</param>
        /// <param name="maxPercentage">max percentage from the price variation</param>
        /// <returns></returns>
        public async Task<List<IBinanceTick>> GetTopPercentages(int numberOfSymbols, string currencySymbol, decimal maxPercentage, List<string> ownedSymbols)
        {
            try
            {
                List<IBinanceTick> allSymbols = await _clientSvc.GetTickers();
                List<IBinanceTick> filteredResult = allSymbols.OrderByDescending(x => x.PriceChangePercent).ToList();
                filteredResult.RemoveAll(x => !x.Symbol.EndsWith(currencySymbol));

                filteredResult = RemoveOwnedCoins(filteredResult, ownedSymbols);

                filteredResult.RemoveAll(x => x.PriceChangePercent > maxPercentage);
                filteredResult = filteredResult.Take(numberOfSymbols).ToList();
                return filteredResult;
            }
            catch (Exception ex)
            {
                _logger.LogError($"ERROR: {DateTime.Now}, metodo: MarketService.GetTopPercentages(), message: {ex.Message}, \n stack: {ex.StackTrace}");
                throw;
            }
        }

        /// <summary>
        /// Returns the data from the last 24h of a specific symbol.
        /// </summary>
        /// <param name="symbol"></param>
        /// <returns></returns>
        public async Task<IBinanceTick> GetSingleTicker(string symbol)
        {
            try
            {
                IBinanceTick data = await _clientSvc.GetTicker(symbol);
                
                return data;
            }
            catch (Exception ex)
            {
                _logger.LogError($"ERROR: {DateTime.Now}, metodo: MarketService.GetSingleTicker(), message: {ex.Message}, \n stack: {ex.StackTrace}");
                return null;
            }
        }

        /// <summary>
        /// Returns the recent data from the list of symbols on 'toMonitor'.
        /// </summary>
        /// <param name="toMonitor">list of coins to be monitored</param>
        /// <returns>Retorna os dados mais recentes das moedas de input</returns>
        public async Task<List<IBinanceTick>> MonitorTopPercentages(List<IBinanceTick> toMonitor)
        {
            List<IBinanceTick> allSymbols = await _clientSvc.GetTickers();
            IEnumerable<IBinanceTick> result = from all in allSymbols
                                               join monitor in toMonitor on all.Symbol equals monitor.Symbol
                                               select all;

            return result.OrderByDescending(x => x.PriceChangePercent).ToList();
        }

        /// <summary>
        /// Sends a buy order.
        /// </summary>
        /// <param name="symbol">symbol that will be bought</param>
        /// <returns></returns>
        public async Task<BinancePlacedOrder> PlaceBuyOrder(string symbol, decimal quantity)
        {
            try
            {
                if (freeMode)
                {
                    BinancePlacedOrder fakeOrder = await PlaceFakeOrder(symbol, quantity);

                    return fakeOrder;
                }

                BinancePlacedOrder order = await _clientSvc.PlaceOrder(symbol, quantity, OrderSide.Buy);

                return order;
            }
            catch (Exception ex)
            {
                _logger.LogError($"ERROR: {DateTime.Now}, metodo: MarketService.PlaceBuyOrder(), message: {ex.Message}, \n stack: {ex.StackTrace}");
                TransmitTradeEvent(TradeEventType.ERROR, $"Date: {DateTime.Now}, message: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Sends a sell order.
        /// </summary>
        /// <param name="symbol">símbolo que será vendido</param>
        /// <param name="quantity">quantidade da moeda que será vendida</param>
        /// <returns></returns>
        public async Task<BinancePlacedOrder> PlaceSellOrder(string symbol, decimal quantity)
        {
            try
            {
                if(freeMode)
                {
                    BinancePlacedOrder fakeOrder = await PlaceFakeOrder(symbol, quantity);

                    return fakeOrder;
                }

                BinancePlacedOrder order = await _clientSvc.PlaceOrder(symbol, quantity, OrderSide.Sell);

                return order;
            }
            catch (Exception ex)
            {
                _logger.LogError($"ERROR: {DateTime.Now}, metodo: MarketService.PlaceSellOrder(), message: {ex.Message}, \n stack: {ex.StackTrace}");
                TransmitTradeEvent(TradeEventType.ERROR, $"Date: {DateTime.Now}, message: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// When running the 'freeMode', this method is used to place a fake buy/sell order.
        /// </summary>
        /// <param name="symbol"></param>
        /// <param name="quantity"></param>
        /// <returns></returns>
        private async Task<BinancePlacedOrder> PlaceFakeOrder(string symbol, decimal quantity)
        {
            BinancePlacedOrder fakeOrder = new BinancePlacedOrder();

            var ticker = await GetSingleTicker(symbol);

            fakeOrder.Price = ticker.AskPrice;
            fakeOrder.Quantity = quantity;

            return fakeOrder;
        }

        /// <summary>
        /// Compares the list of current data from the coins against the list of previously validated, verifies if exists a valorization of X% to try to identify 
        /// an upward trend, consequently, a possible purchase opportunitie.
        /// </summary>
        /// <param name="currentData">current market data</param>
        /// <param name="previousData">previously separated data</param>
        /// <returns>list of possible opportunities</returns>
        public List<IBinanceTick> CheckOpportunities(List<IBinanceTick> currentData, List<IBinanceTick> previousData)
        {
            List<IBinanceTick> result = new List<IBinanceTick>();

            var res = from obj in currentData
                      join prev in previousData on obj.Symbol equals prev.Symbol
                      where obj.PriceChangePercent - prev.PriceChangePercent > (decimal)0.3
                      //where obj.PriceChangePercent - prev.PriceChangePercent > (decimal)0.4 && prev.WeightedAveragePrice < obj.AskPrice
                      select prev;

            result = res.ToList();

            //foreach(var obj in previousData)
            //{
            //    var current = currentData.Find(x => x.Symbol == obj.Symbol);
            //    // renovar os precos minimos e maximos 
            //}

            return result;
        }

        /// <summary>
        /// Removes from the list of symbols (allSymbols) the symbols that already have an open position (ownedSymbols).
        /// </summary>
        /// <param name="allSymbols"></param>
        /// <param name="ownedSymbols"></param>
        /// <returns>lista atualizada</returns>
        private List<IBinanceTick> RemoveOwnedCoins(List<IBinanceTick> allSymbols, List<string> ownedSymbols)
        {
            for (int i = 0; i < ownedSymbols.Count; i++)
            {
                string current = ownedSymbols[i];

                //allSymbols.RemoveAll(x => x.Symbol.StartsWith(current));
                allSymbols.RemoveAll(x => x.Symbol.Contains(current));
            }

            return allSymbols;
        }

        private async void TransmitTradeEvent(TradeEventType type, string message)
        {
            bool sent = await _eventsOutput.SendEvent(new TradeEvent(type, DateTime.Now, message));
        }
    }
}
