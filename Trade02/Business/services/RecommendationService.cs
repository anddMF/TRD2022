using Binance.Net.Enums;
using Binance.Net.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Trade02.Business.services.Interfaces;
using Trade02.Infra.DAL;
using Trade02.Infra.DAL.Interfaces;
using Trade02.Models.CrossCutting;
using Trade02.Models.Trade;

namespace Trade02.Business.services
{
    public class RecommendationService : IRecommendationService
    {
        private static IAPICommunication _clientSvc;
        private readonly ILogger _logger;

        private readonly int daysToAnalyze = AppSettings.TradeConfiguration.DaysToAnalyze + 1;

        private readonly bool dayConfig = AppSettings.EngineConfiguration.Day;
        private readonly bool hourConfig = AppSettings.EngineConfiguration.Hour;
        private readonly bool minuteConfig = AppSettings.EngineConfiguration.Minute;
        private readonly bool maConfig = AppSettings.EngineConfiguration.MovingAverage;

        public RecommendationService(ILogger<RecommendationService> logger, IAPICommunication clientSvc)
        {
            _logger = logger;
            _clientSvc = clientSvc;
        }

        // entre 40 e 70
        public bool CalculateRSI(IEnumerable<IBinanceKline> klines, int period = 14)
        {
            var results = new List<RSIResult>();

            var priceChanges = klines.Select(k => k.Close - k.Open).ToList();

            decimal avgGain = 0;
            decimal avgLoss = 0;

            for (var i = 1; i <= period; i++)
            {
                var change = priceChanges[i];
                if (change > 0)
                    avgGain += change;
                else
                    avgLoss -= change; // Note que usamos um valor positivo

                results.Add(new RSIResult { Timestamp = klines.ElementAt(i).CloseTime, RSI = 0 });
            }

            avgGain /= period;
            avgLoss /= period;

            for (var i = period + 1; i < klines.Count(); i++)
            {
                var change = priceChanges[i];
                avgGain = (avgGain * (period - 1) + (change > 0 ? change : 0)) / period;
                avgLoss = (avgLoss * (period - 1) + (change < 0 ? -change : 0)) / period;

                var relativeStrength = avgGain / avgLoss;
                var rsi = 100 - (100 / (1 + relativeStrength));

                results.Add(new RSIResult { Timestamp = klines.ElementAt(i).CloseTime, RSI = (double)rsi });
            }
            
            return CheckRSIWindow(results);
        }

        bool CheckRSIWindow(List<RSIResult> rsiResults, int numeroItensVerificar = 5, double limiteInferior = 40, double limiteSuperior = 70)
        {
            // Verificar se há pelo menos 'numeroItensVerificar' resultados
            if (rsiResults.Count < numeroItensVerificar)
                return false;

            // Pegar os últimos 'numeroItensVerificar' resultados
            var ultimosResultados = rsiResults.Take(numeroItensVerificar);

            // Verificar se todos os RSI estão dentro da faixa especificada
            return ultimosResultados.All(rsi => rsi.RSI >= limiteInferior && rsi.RSI <= limiteSuperior);
        }

        /// <summary>
        /// Iterates on the list 'currentMarket' and try to identify opportunities using the timed klines from the asset
        /// </summary>
        /// <param name="currentMarket">filtered list of assets</param>
        /// <param name="days">switch to check for opportunities of type 'days'</param>
        /// <param name="hours">switch to check for opportunities of type 'hours'</param>
        /// <param name="minutes">switch to check for opportunities of type 'minutes'</param>
        /// <returns></returns>
        public async Task<OpportunitiesResponse> CheckOpportunitiesByKlines(List<IBinanceTick> currentMarket)
        {
            // falta uma validação para recomendações repetidas em diferentes tipos
            HashSet<string> alreadyUsed = new HashSet<string>();

            List<IBinanceTick> daysList = new List<IBinanceTick>();
            List<IBinanceTick> hoursList = new List<IBinanceTick>();
            List<IBinanceTick> minutesList = new List<IBinanceTick>();

            // o filtro do dia pega so de ontem, entao a moeda pode estar em queda hoje que ele nao vai pegar
            if (dayConfig)
            {
                for (int i = 0; i < currentMarket.Count; i++)
                {
                    var current = currentMarket[i];
                    bool opportunity = await IsAKlineOpportunity(current.Symbol, KlineInterval.OneDay, daysToAnalyze);

                    if (opportunity && !alreadyUsed.Contains(current.Symbol))
                    {
                        daysList.Add(current);
                        alreadyUsed.Add(current.Symbol);
                    }
                }
            }


            if (hourConfig)
            {
                for (int i = 0; i < currentMarket.Count; i++)
                {
                    var current = currentMarket[i];
                    bool opportunity = await IsAKlineOpportunity(current.Symbol, KlineInterval.OneHour, daysToAnalyze);

                    if (opportunity && !alreadyUsed.Contains(current.Symbol))
                    {
                        hoursList.Add(current);
                        alreadyUsed.Add(current.Symbol);
                    }
                }
            }


            if (minuteConfig)
            {
                for (int i = 0; i < currentMarket.Count; i++)
                {
                    var current = currentMarket[i];
                    bool opportunity = await IsAKlineOpportunity(current.Symbol, KlineInterval.OneMinute, 3);

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
        /// Verifies if the symbol has a favorable buy status based on it's last klines.
        /// </summary>
        /// <param name="symbol"></param>
        /// <param name="interval"></param>
        /// <param name="period"></param>
        /// <returns></returns>
        public async Task<bool> IsAKlineOpportunity(string symbol, KlineInterval interval, int period)
        {
            // separa os últimos X dias de klines
            var ogKlines = await _clientSvc.GetKlines(symbol, interval);
            var klines = ogKlines.TakeLast(period).ToList();
            //var rsi = CalculateRSI(klines);
            //Console.WriteLine($"RSI: {symbol}; {rsi}");

            decimal max = decimal.MinValue;

            int flags = 0;
            bool avg = false;

            // maybe already dicard the ones that had yesterday on a lower 'high' than today
            // identifica uma oportunidade de uma moeda que está renovando suas máximas consequentemente. 
            // o i < klines.Count pode ser Count - 1 se o teste for feito de manhã, mas deixa .Count se for a noite
            for (int i = 0; i < klines.Count; i++)
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

            if (maConfig)
            {
                avg = SuperiorMovingAverage(ogKlines);
                Console.WriteLine($"\tMA: {symbol}; {avg}");
                return avg;
            }

            return true;
        }

        /// <summary>
        /// Responsible to verify on the list of recommendations if contains positions that were already sold before, if so, only maintains in the list the ones that have a price 1% higher compared to the sold price.
        /// </summary>
        /// <param name="opp">list of recommendations</param>
        /// <param name="assetList">list of already sold positions</param>
        /// <returns></returns>
        public OpportunitiesResponse RepurchaseValidation(OpportunitiesResponse opp, List<Position> assetList)
        {
            //var dayList = assetList.FindAll(x => x.Type == RecommendationType.Day);
            //var hourList = assetList.FindAll(x => x.Type == RecommendationType.Hour);
            //var minuteList = assetList.FindAll(x => x.Type == RecommendationType.Minute);

            opp.Days = RepurchasePercentageValidation(opp.Days, assetList);
            opp.Hours = RepurchasePercentageValidation(opp.Hours, assetList);
            opp.Minutes = RepurchasePercentageValidation(opp.Minutes, assetList);

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

            if (toDelete.Count > 0)
            {
                var left = from list in opp
                           where !toDelete.Any(x => x.Symbol == list.Symbol)
                           select list;

                opp = left.ToList();
            }

            return opp;
        }

        /// <summary>
        /// Verifies if the shortest MA (moving average) is, at least, 1% above compared to the longest one. If so, returns true, and returns false otherwise.
        /// </summary>
        /// <param name="klines"></param>
        /// <returns></returns>
        private bool SuperiorMovingAverage(List<IBinanceKline> klines)
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

        private decimal CalculateMovingAverage(int period, List<IBinanceKline> klines)
        {
            decimal sum = 0;
            decimal average = 0;
            klines = klines.TakeLast(period).ToList();

            for (int i = 0; i < klines.Count; i++)
            {
                decimal currentAvg = (klines[i].High + klines[i].Low) / 2;
                sum += currentAvg;
            }

            average = sum / period;

            return average;
        }

    }

    public class RSIResult
    {
        public DateTime Timestamp { get; set; }
        public double RSI { get; set; }
    }
}
