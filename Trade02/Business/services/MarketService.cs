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

        private readonly int daysToAnalyze = AppSettings.TradeConfiguration.DaysToAnalyze + 1;

        public MarketService(IHttpClientFactory clientFactory, ILogger<Worker> logger)
        {
            _logger = logger;
            _clientSvc = new APICommunication(clientFactory);
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
                // preciso ver como confirmar que a operação já foi executada, não a ordem em si
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
                // tratamentos específicos para venda
                BinancePlacedOrder order = await _clientSvc.PlaceOrder(symbol, quantity, OrderSide.Sell);

                return order;
            }
            catch (Exception ex)
            {
                _logger.LogError($"ERROR: {DateTime.Now}, metodo: MarketService.PlaceSellOrder(), message: {ex.Message}, \n stack: {ex.StackTrace}");
                return null;
            }
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

        public async Task<OpportunitiesResponse> CheckOppotunitiesByKlines(List<IBinanceTick> currentMarket, bool days, bool hours, bool minutes)
        {
            // falta uma validação para recomendações repetidas em diferentes tipos
            HashSet<string> alreadyUsed = new HashSet<string>();

            List<IBinanceTick> daysList = new List<IBinanceTick>();
            List<IBinanceTick> hoursList = new List<IBinanceTick>();
            List<IBinanceTick> minutesList = new List<IBinanceTick>();

            // o filtro do dia pega so de ontem, entao a moeda pode estar em queda hoje que ele nao vai pegar
            if (days)
            {
                for (int i = 0; i < currentMarket.Count; i++)
                {
                    var current = currentMarket[i];
                    bool opportunity = await IsAKlineOpportunitie(current.Symbol, KlineInterval.OneDay, daysToAnalyze);

                    if (opportunity && !alreadyUsed.Contains(current.Symbol))
                    {
                        daysList.Add(current);
                        alreadyUsed.Add(current.Symbol);
                    }
                }
            }


            if (hours)
            {
                for (int i = 0; i < currentMarket.Count; i++)
                {
                    var current = currentMarket[i];
                    bool opportunity = await IsAKlineOpportunitie(current.Symbol, KlineInterval.OneHour, 3);

                    if (opportunity && !alreadyUsed.Contains(current.Symbol))
                    {
                        hoursList.Add(current);
                        alreadyUsed.Add(current.Symbol);
                    }
                }
            }


            if (minutes)
            {
                for (int i = 0; i < currentMarket.Count; i++)
                {
                    var current = currentMarket[i];
                    bool opportunity = await IsAKlineOpportunitie(current.Symbol, KlineInterval.FifteenMinutes, 3);

                    if (opportunity && !alreadyUsed.Contains(current.Symbol))
                    {
                        minutesList.Add(current);
                        alreadyUsed.Add(current.Symbol);
                    }
                }
            }

            return new OpportunitiesResponse(daysList, hoursList, minutesList);
        }

        /// <summary>
        /// Faz uma atualização na lista de recomendações, validando se existem moedas já vendidas nas mesmas, caso tenha, somente mantém nas listas caso esteja 1% acima do valor que foi vendida.
        /// </summary>
        /// <param name="opp">lista de recomendações</param>
        /// <param name="assetList">lista de moedas que foram vendidas</param>
        /// <returns></returns>
        public OpportunitiesResponse RepurchaseValidation(OpportunitiesResponse opp, List<Position> assetList)
        {
            var dayList = assetList.FindAll(x => x.Type == RecommendationType.Day);
            var hourList = assetList.FindAll(x => x.Type == RecommendationType.Hour);
            var minuteList = assetList.FindAll(x => x.Type == RecommendationType.Minute);

            opp.Days = RepurchasePercentageValidation(opp.Days, dayList);
            opp.Hours = RepurchasePercentageValidation(opp.Hours, hourList);
            opp.Minutes = RepurchasePercentageValidation(opp.Minutes, minuteList);

            return opp;
        }

        private List<IBinanceTick> RepurchasePercentageValidation(List<IBinanceTick> opp, List<Position> assetList)
        {
            var toDelete = new List<IBinanceTick>();
            for (int i = 0; i < opp.Count; i++)
            {
                var current = opp[i];
                int assetIndex = assetList.FindIndex(x => x.Symbol == current.Symbol);
                if (assetIndex > -1)
                {
                    decimal valorization = ((current.AskPrice - assetList[assetIndex].LastPrice) / assetList[assetIndex].LastPrice) * 100;

                    if (valorization <= 1)
                        toDelete.Add(current);
                }
            }

            if(toDelete.Count > 0)
            {
                var left = from list in opp
                            where !toDelete.Any(x => x.Symbol == list.Symbol)
                            select list;

                opp = left.ToList();
            }

            return opp;
        }

        public async Task<bool> IsAKlineOpportunitie(string symbol, KlineInterval interval, int period)
        {
            // separa os últimos X dias de klines
            var ogKlines = await _clientSvc.GetKlines(symbol, interval);
            var klines = ogKlines.TakeLast(period).ToList();

            decimal max = decimal.MinValue;

            int flags = 0;
            bool avg = false;

            // maybe already dicard the ones tha had yesterday on a lower high than today
            // identifica uma oportunidade de uma moeda que está renovando suas máximas consequentemente. 
            // o i < klines.Count pode ser Count - 1 se o teste for feito de manhã, mas deixa .Count se for a noite
            for (int i = 0; i < klines.Count ; i++)
            {
                var current = klines[i];

                if (current.High > max)
                {
                    max = current.High;
                }
                // se não renovar a máxima
                else
                {
                    // verifica se já não tinha renovado antes
                    if (flags >= 1)
                    {
                        // already has enough flags to discard this opportunity
                        return false;
                    }
                    // verifica se não está um dia antes de hoje
                    if (flags == 0 && i < klines.Count - 2)
                        flags++;
                    
                    // verifica se está um dia antes ou hoje
                    if (flags == 0 && i >= klines.Count - 2)
                    {
                        // down on the day before, discard the opportunity
                        return false;
                    }
                }
            }

            // se chegou até aqui é porque ainda é valido
            avg = SuperiorMovingAverage(ogKlines);

            return avg;
        }

        public bool SuperiorMovingAverage(List<IBinanceKline> klines)
        {
            decimal avg1 = CalculateMovingAverage(5, klines);
            decimal avg2 = CalculateMovingAverage(10, klines);

            if (avg1 > avg2)
            {
                decimal percentage = ((avg1 - avg2) / avg2) * 100;

                return percentage > 1;
            }
            else
                return false;
        }

        public decimal CalculateMovingAverage(int period, List<IBinanceKline> klines)
        {
            decimal sum = 0;
            decimal average = 0;
            klines = klines.TakeLast(period).ToList();

            for(int i = 0; i < klines.Count; i++)
            {
                decimal currentAvg = (klines[i].High + klines[i].Low) / 2;
                sum += currentAvg;
            }

            average = sum / period;

            return average;
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
