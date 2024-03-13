using Binance.Net.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Trade02.Business.services;
using Trade02.Business.services.Interfaces;
using Trade02.Infra.Cross;
using Trade02.Models.CrossCutting;
using Trade02.Models.Trade;

namespace Trade02
{
    public class Worker : BackgroundService
    {
        private readonly ILogger _logger;
        private static IMarketService _marketSvc;
        private static IPortfolioService _portfolioSvc;
        private static IRecommendationService _recSvc;
        private static IEventsOutput _eventsOutput;

        private readonly string currency = AppSettings.TradeConfiguration.Currency;
        private readonly int maxToMonitor = AppSettings.TradeConfiguration.MaxToMonitor;
        private readonly decimal maxSearchPercentage = AppSettings.TradeConfiguration.MaxSearchPercentage;
        private readonly int maxOpenPositions = AppSettings.TradeConfiguration.MaxOpenPositions;

        private decimal currentProfit = AppSettings.TradeConfiguration.CurrentProfit;

        public Worker(ILogger<Worker> logger, IPortfolioService portfolioService, IMarketService marketSvc, IRecommendationService recSvc, IEventsOutput eventsOutput)
        {
            _logger = logger;
            _marketSvc = marketSvc;
            _portfolioSvc = portfolioService;
            _recSvc = recSvc;
            _eventsOutput = eventsOutput;
        }

        private async void TransmitEvent(TradeEventType type, string message, Position position = null)
        {
            bool sent = await _eventsOutput.SendEvent(new TradeEvent(type, DateTime.Now, message, position));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Console.WriteLine("---------------#------ TRD2022 ------#----------------");
            try
            {
                bool runner = true;

                List<Position> toMonitor = new List<Position>();

                var alreadyOpenPositions = _portfolioSvc.GetLastPositions();
                List<Position> openPositions = alreadyOpenPositions == null ? new List<Position>() : alreadyOpenPositions;

                List<string> ownedSymbols = AppSettings.TradeConfiguration.OwnedSymbols;

                List<IBinanceTick> currentMarket = await _marketSvc.GetTopPercentages(maxToMonitor, currency, maxSearchPercentage, ownedSymbols);
                var opp = await _recSvc.CheckOpportunitiesByKlines(currentMarket);

                Console.WriteLine("----###### Initial list captured ######----\n");
                TransmitEvent(TradeEventType.START, "");
                while (runner)
                {
                    Console.WriteLine($"\n----###### WORKER: positions {openPositions.Count} - {DateTime.Now.ToString("H:mm:ss")} ######----");

                    var manager = await _portfolioSvc.ManagePosition(opp, openPositions, toMonitor);

                    openPositions = manager.OpenPositions;
                    toMonitor = manager.ToMonitor;

                    await Task.Delay(1000, stoppingToken);

                    if (AppSettings.TradeConfiguration.CurrentProfit < AppSettings.TradeConfiguration.MaxProfit)
                    {
                        if (openPositions.Count >= maxOpenPositions)
                            _logger.LogWarning($"#### #### #### #### #### #### ####\n\t#### Reached the maximum number of open positions ####\n\t#### #### #### #### #### #### ####\n");
                        else
                        {
                            opp = await _recSvc.CheckOpportunitiesByKlines(currentMarket);
                            if (toMonitor.Count > 0)
                                opp = _recSvc.RepurchaseValidation(opp, toMonitor);
                        }
                    } else if (openPositions.Count == 0){
                        _logger.LogInformation($"\n\t ----###### Reached the maximum profit ######---- \n % {Utils.FormatDecimal(AppSettings.TradeConfiguration.CurrentProfit)} \n USDT: {Utils.FormatDecimal(AppSettings.TradeConfiguration.CurrentUSDTProfit)}");
                        TransmitEvent(TradeEventType.FINISH, $"Reached the maximum profit: {Utils.FormatDecimal(AppSettings.TradeConfiguration.CurrentProfit)}:{Utils.FormatDecimal(AppSettings.TradeConfiguration.CurrentUSDTProfit)}");
                        runner = false;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"ERROR: {DateTime.Now}, metodo: ExecuteAsync(), message: {ex.Message}, \n stack: {ex.StackTrace}");
                TransmitEvent(TradeEventType.FINISH, "");
                throw ex;
            }
        }
    }
}
