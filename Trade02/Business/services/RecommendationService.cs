using Binance.Net.Enums;
using Binance.Net.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
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

        public async Task<OpportunitiesResponse> CheckOppotunitiesByKlines(List<IBinanceTick> currentMarket, bool days, bool hours, bool minutes)
        {
            // falta uma validação para recomendações repetidas em diferentes tipos
            HashSet<string> alreadyUsed = new HashSet<string>();

            List<IBinanceTick> daysList = new List<IBinanceTick>();
            List<IBinanceTick> hoursList = new List<IBinanceTick>();
            List<IBinanceTick> minutesList = new List<IBinanceTick>();

            // o filtro do dia pega so de ontem, entao a moeda pode estar em queda hoje que ele nao vai pegar
            if (days && dayConfig)
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


            if (hours && hourConfig)
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


            if (minutes && minuteConfig)
            {
                for (int i = 0; i < currentMarket.Count; i++)
                {
                    var current = currentMarket[i];
                    bool opportunity = await IsAKlineOpportunitie(current.Symbol, KlineInterval.OneMinute, 3);

                    if (opportunity && !alreadyUsed.Contains(current.Symbol))
                    {
                        minutesList.Add(current);
                        alreadyUsed.Add(current.Symbol);
                    }
                }
            }

            return new OpportunitiesResponse(daysList, hoursList, minutesList);
        }

        public async Task<bool> IsAKlineOpportunitie(string symbol, KlineInterval interval, int period)
        {
            // separa os últimos X dias de klines
            var ogKlines = await _clientSvc.GetKlines(symbol, interval);
            var klines = ogKlines.TakeLast(period).ToList();

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
                return avg;
            }

            return true;
        }

        /// <summary>
        /// Faz uma atualização na lista de recomendações, validando se existem moedas já vendidas nas mesmas, caso tenha, somente mantém nas listas caso esteja 1% acima do valor que foi vendida.
        /// </summary>
        /// <param name="opp">lista de recomendações</param>
        /// <param name="assetList">lista de moedas que foram vendidas</param>
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
        /// Verifica se a MA (média móvel) mais curta está, pelo menos, 1% acima da MA mais longa. Caso esteja, retorna true, do contrário, retorna false.
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
}
