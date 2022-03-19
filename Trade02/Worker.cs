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
using Trade02.Models.CrossCutting;
using Trade02.Models.Trade;

namespace Trade02
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private static MarketService _marketSvc;
        private static PortfolioService _portfolioSvc;

        private readonly string currency = AppSettings.TradeConfiguration.Currency;
        private readonly int maxToMonitor = AppSettings.TradeConfiguration.MaxToMonitor;
        private readonly decimal maxSearchPercentage = AppSettings.TradeConfiguration.MaxSearchPercentage;
        private readonly int maxOpenPositions = AppSettings.TradeConfiguration.MaxOpenPositions;

        private decimal currentProfit = AppSettings.TradeConfiguration.CurrentProfit;

        public Worker(ILogger<Worker> logger, IHttpClientFactory clientFactory)
        {
            _logger = logger;
            _marketSvc = new MarketService(clientFactory, logger);
            _portfolioSvc = new PortfolioService(clientFactory, logger);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Console.WriteLine("---------------#------ TRD2022 ------#----------------");
            try
            {
                bool runner = true;

                bool days = true;
                bool hours = true;
                bool minutes = true;

                List<Position> openPositions = new List<Position>();
                var open = _portfolioSvc.GetLastPositions();
                openPositions = open == null ? new List<Position>() : open;

                List<Position> toMonitor = new List<Position>();

                List<string> ownedSymbols = AppSettings.TradeConfiguration.OwnedSymbols;

                List<IBinanceTick> currentMarket = await _marketSvc.GetTopPercentages(maxToMonitor, currency, maxSearchPercentage, ownedSymbols);
                var opp = await _marketSvc.CheckOppotunitiesByKlines(currentMarket, true, true, true);
                

                Console.WriteLine("----------------- Lista inicial capturada ------------------\n");

                days = true;
                hours = true;
                minutes = true;

                // esse loop está aqui por conta do retorno que vem no wallet
                //foreach (Position pos in openPositions)
                //{
                //    if (pos.Type == RecommendationType.Day)
                //        days = false;

                //    if (pos.Type == RecommendationType.Hour)
                //        hours = false;

                //    if (pos.Type == RecommendationType.Minute)
                //        minutes = false;
                //}

                while (runner)
                {
                    
                    Console.WriteLine($"----###### WORKER: posicoes {openPositions.Count}\n");

                    // manage positions recebendo as recoendações e operações em aperto
                    var manager = await _portfolioSvc.ManagePosition(opp, openPositions, toMonitor);

                    openPositions = manager.OpenPositions;

                    // esse controle do balanço da carteira tem que estar no managePortfolio
                    //foreach (Position pos in openPositions)
                    //{
                    //    if (pos.Type == RecommendationType.Day)
                    //        days = false;

                    //    if (pos.Type == RecommendationType.Hour)
                    //        hours = false;

                    //    if (pos.Type == RecommendationType.Minute)
                    //        minutes = false;
                    //}
                    //opp = manager.Opportunities;

                    toMonitor = manager.ToMonitor;

                    // colocado aqui para não ter o delay entre a recomendação e o managePosition
                    await Task.Delay(20000, stoppingToken);

                    if (AppSettings.TradeConfiguration.CurrentProfit < AppSettings.TradeConfiguration.MaxProfit)
                    {
                        if (!days && !hours && !minutes)
                            _logger.LogWarning($"#### #### #### #### #### #### ####\n\t#### Atingido numero maximo de posicoes em aberto ####\n\t#### #### #### #### #### #### ####\n");
                        else
                        {
                            opp = await _marketSvc.CheckOppotunitiesByKlines(currentMarket, days, hours, minutes);
                            if (toMonitor.Count > 0)
                                opp = _marketSvc.RepurchaseValidation(opp, toMonitor);
                        }
                    } else if (openPositions.Count == 0){
                        _logger.LogInformation($"\n\t ###### Lucro maximo atingido ######");
                        runner = false;
                    }

                    days = true;
                    hours = true;
                    minutes = true;
                }

            }
            catch (Exception ex)
            {
                _logger.LogError($"ERROR: {DateTime.Now}, metodo: ExecuteAsync(), message: {ex.Message}, \n stack: {ex.StackTrace}");
                throw ex;
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
                await Task.Delay(1000, stoppingToken);
            }
        }
    }
}
