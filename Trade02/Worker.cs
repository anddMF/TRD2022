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
        private static RecommendationService _recSvc;

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
            _recSvc = new RecommendationService(clientFactory, logger);
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
                var opp = await _recSvc.CheckOppotunitiesByKlines(currentMarket, true, true, true);
                

                Console.WriteLine("----------------- Lista inicial capturada ------------------\n");

                // TODO: avaliar um jeito para desligar as recomendações que já estão cheias, diminuindo, assim, processamento e tempo
                days = true;
                hours = true;
                minutes = true;


                while (runner)
                {
                    
                    Console.WriteLine($"----###### WORKER: posicoes {openPositions.Count}\n");

                    // manage positions recebendo as recoendações e operações em aperto
                    var manager = await _portfolioSvc.ManagePosition(opp, openPositions, toMonitor);

                    openPositions = manager.OpenPositions;
                    toMonitor = manager.ToMonitor;

                    // colocado aqui para não ter o delay entre a recomendação e o managePosition
                    await Task.Delay(20000, stoppingToken);

                    if (AppSettings.TradeConfiguration.CurrentProfit < AppSettings.TradeConfiguration.MaxProfit)
                    {
                        if (openPositions.Count >= maxOpenPositions)
                            _logger.LogWarning($"#### #### #### #### #### #### ####\n\t#### Atingido numero maximo de posicoes em aberto ####\n\t#### #### #### #### #### #### ####\n");
                        else
                        {
                            // TODO: avaliar um jeito para desligar as recomendações que já estão cheias, diminuindo, assim, processamento e tempo
                            opp = await _recSvc.CheckOppotunitiesByKlines(currentMarket, days, hours, minutes);
                            if (toMonitor.Count > 0)
                                opp = _recSvc.RepurchaseValidation(opp, toMonitor);
                        }
                    } else if (openPositions.Count == 0){
                        _logger.LogInformation($"\n\t ###### Lucro maximo atingido ######");
                        runner = false;
                    }
                }

            }
            catch (Exception ex)
            {
                _logger.LogError($"ERROR: {DateTime.Now}, metodo: ExecuteAsync(), message: {ex.Message}, \n stack: {ex.StackTrace}");
                throw ex;
            }
        }
    }
}
