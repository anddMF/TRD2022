using Binance.Net.Enums;
using Binance.Net.Interfaces;
using Binance.Net.Objects.Spot.SpotData;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Trade02.Business.services.Interfaces;
using Trade02.Infra.DAL.Interfaces;
using Trade02.Models.CrossCutting;

namespace Trade02.Business.services
{
    /// <summary>
    /// Responsável por manipular dados de mercado.
    /// </summary>
    public class MarketService : IMarketService
    {
        private static IAPICommunication _clientSvc;
        private readonly ILogger _logger;

        private readonly bool freeMode = AppSettings.TradeConfiguration.FreeMode;

        public MarketService(ILogger<MarketService> logger, IAPICommunication clientSvc)
        {
            _logger = logger;
            _clientSvc = clientSvc;
        }

        /// <summary>
        /// Retorna os símbolos com a maior valorização positiva ordenados na decrescente.
        /// </summary>
        /// <param name="numberOfSymbols">quantidade de símbolos que serão retornados</param>
        /// <param name="currencySymbol">símbolo da moeda que será utilizada para a compra</param>
        /// <param name="maxPercentage">porcentagem máxima da variação de preço</param>
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
        /// Retorna os dados das últimas 24h de um determinado símbolo.
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
        /// Retorna os dados atuais das moedas enviadas no input.
        /// </summary>
        /// <param name="toMonitor">List de moedas para serem monitoradas</param>
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
        /// Envia ordem de compra.
        /// </summary>
        /// <param name="symbol">símbolo que será comprado</param>
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
                return null;
            }
        }

        /// <summary>
        /// Envia uma ordem de venda
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
                return null;
            }
        }

        private async Task<BinancePlacedOrder> PlaceFakeOrder(string symbol, decimal quantity)
        {
            BinancePlacedOrder fakeOrder = new BinancePlacedOrder();

            var ticker = await GetSingleTicker(symbol);

            fakeOrder.Price = ticker.AskPrice;
            fakeOrder.Quantity = quantity;

            return fakeOrder;
        }

        /// <summary>
        /// Cruza as listas de dados atuais das moedas e os anteriormente validados, verifica se existe uma valorização de X% para identificar uma tendencia de subida
        /// e, por consequência, uma possível compra. Retorna a lista de moedas que atendam a estes requisitos.
        /// </summary>
        /// <param name="currentData">dados atuais do market</param>
        /// <param name="previousData">dados anteriormente separados</param>
        /// <returns>Lista com as oportunidades de possíveis compras</returns>
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
        /// Remove da lista de símbolos as moedas que já possuem posição em aberto.
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
    }
}
