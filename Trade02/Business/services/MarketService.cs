using Binance.Net.Enums;
using Binance.Net.Interfaces;
using Binance.Net.Objects.Spot.SpotData;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Trade02.Infra.DAL;
using Trade02.Models.CrossCutting;
using Trade02.Models.Trade;

namespace Trade02.Business.services
{
    /// <summary>
    /// Responsável por manipular dados de mercado.
    /// </summary>
    public class MarketService
    {
        private static APICommunication _clientSvc;
        private readonly ILogger<Worker> _logger;

        public MarketService(IHttpClientFactory clientFactory, ILogger<Worker> logger)
        {
            _logger = logger;
            _clientSvc = new APICommunication(clientFactory);
        }

        /// <summary>
        /// Retorna os símbolos com a maior valorização positiva ordenados na decrescente.
        /// </summary>
        /// <param name="numberOfSymbols">quantidade de símbolos que serão retornados</param>
        /// <param name="currencySymbol">símbola da moeda que será utilizada para a compra</param>
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

                // maxPercentage=10; faz sentido ter esse limite porque as chances de pegar uma moeda que já cresceu o que tinha que crescer depois
                // de 10% é maior do que uma moeda que valorizou menos de 10%
                filteredResult.RemoveAll(x => x.PriceChangePercent > maxPercentage);
                filteredResult = filteredResult.Take(numberOfSymbols).ToList();
                return filteredResult;
            }
            catch (Exception ex)
            {
                _logger.LogError($"ERROR: {DateTimeOffset.Now}, metodo: MarketService.GetTopPercentages(), message: {ex.Message}");
                throw;
            }
        }

        public async Task<IBinanceTick> GetSingleTicker(string symbol)
        {
            try
            {
                IBinanceTick data = await _clientSvc.GetTicker(symbol);

                return data;
            }
            catch (Exception ex)
            {
                _logger.LogError($"ERROR: {DateTimeOffset.Now}, metodo: MarketService.GetSingleTicker(), message: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Retorna somente os dados da lista de moedas no input toMonitor.
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
        /// Envia ordem de compra
        /// </summary>
        /// <param name="symbol"></param>
        /// <returns></returns>
        public async Task<BinancePlacedOrder> PlaceBuyOrder(string symbol)
        {
            // calculo da quantidade
            decimal quantity = 10;
            try
            {
                // preciso ver como confirmar que a operação já foi executada, não a ordem em si
                BinancePlacedOrder order = await _clientSvc.PlaceOrder(symbol, quantity, OrderSide.Buy);

                return order;
            }
            catch (Exception ex)
            {
                _logger.LogError($"ERROR: {DateTimeOffset.Now}, metodo: MarketService.PlaceBuyOrder(), message: {ex.Message}");
                // não vai subir com erro pra não parar a aplicação
                return null;
            }
        }

        public async Task<BinancePlacedOrder> PlaceSellOrder(string symbol, decimal quantity)
        {
            try
            {
                // tratamentos específicos para venda
                BinancePlacedOrder order = await _clientSvc.PlaceOrder(symbol, quantity, OrderSide.Sell);

                return order;
            }
            catch (Exception ex)
            {
                _logger.LogError($"ERROR: {DateTimeOffset.Now}, metodo: MarketService.PlaceSellOrder(), message: {ex.Message}");
                // não vai subir com erro pra não parar a aplicação
                return null;
            }
        }

        public async Task<OrderEngine> ExecuteOrder(List<Position> openPositions, List<string> symbolsOwned, List<IBinanceTick> oportunities, List<IBinanceTick> response, int minute, bool debug = false)
        {
            for (int i = 0; i < oportunities.Count; i++)
            {
                var current = response.Find(x => x.Symbol == oportunities[i].Symbol);

                var count = current.PriceChangePercent - oportunities[i].PriceChangePercent;
                _logger.LogInformation($"COMPRA: {DateTimeOffset.Now}, moeda: {oportunities[i].Symbol}, current percentage: {current.PriceChangePercent}, percentage change in {minute}: {count}, value: {oportunities[i].AskPrice}");

                if (!debug)
                {
                    // controle de numero maximo de posicoes em aberto
                    if(openPositions.Count < 5)
                    {
                        // executa a compra
                        var order = await PlaceBuyOrder(current.Symbol);
                        if (order == null)
                        {
                            // não executou, eu faço log do problema na tela mas ainda tenho que ver os possíveis erros pra saber como tratar
                            _logger.LogWarning($"### compra de {current.Symbol} NAO EXECUTADA ###");
                        }
                        else
                        {
                            symbolsOwned.Add(current.Symbol);

                            // adicionar mais validações pois o quantity pode não ter sido 100% filled
                            openPositions.Add(new Position(current, order.Price, order.Quantity));
                        }
                    }
                    else
                    {
                        return new OrderEngine(openPositions, symbolsOwned);
                    }
                }

            }

            return new OrderEngine(openPositions, symbolsOwned);
        }

        /// <summary>
        /// Cruza as listas de dados atuais das moedas e os anteriormente validados, verifica se existe uma valorização de X% para identificar uma tendencia de subida
        /// e, por consequência, uma possível compra. Retorna a lista de moedas que atendam a estes requisitos.
        /// </summary>
        /// <param name="currentData"></param>
        /// <param name="previousData"></param>
        /// <returns>Lista com as oportunidades de possíveis compras</returns>
        public List<IBinanceTick> CheckOportunities(List<IBinanceTick> currentData, List<IBinanceTick> previousData)
        {
            List<IBinanceTick> result = new List<IBinanceTick>();

            var res = from obj in currentData
                      join prev in previousData on obj.Symbol equals prev.Symbol
                      where obj.PriceChangePercent - prev.PriceChangePercent > 1
                      select prev;

            result = res.ToList();

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
                allSymbols.RemoveAll(x => x.Symbol == current);
            }

            return allSymbols;
        }
    }
}
