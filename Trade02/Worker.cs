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

                List<string> ownedSymbols = AppSettings.TradeConfiguration.OwnedSymbols;

                List<IBinanceTick> currentMarket = await _marketSvc.GetTopPercentages(maxToMonitor, currency, maxSearchPercentage, ownedSymbols);
                var opp = await _marketSvc.CheckOppotunitiesByKlines(currentMarket, true, true, true);
                

                Console.WriteLine("----------------- Lista incial capturada ------------------");
                Console.WriteLine();

                while (runner)
                {
                    days = true;
                    hours = true;
                    minutes = true;
                    Console.WriteLine($"----###### WORKER: posicoes {openPositions.Count}\n");

                    // manage positions recebendo as recoendações e operações em aperto
                    var manager = await _portfolioSvc.ManagePosition(opp, openPositions);

                    
                    openPositions = manager.OpenPositions;

                    // TODO: na lista de toMonitor preciso considerar qual o type que tem lá e não fazer compras pois ainda está obervando aquele type
                    // caso ainda tenha recomendação não executada, chama opp by klines com as que faltam
                    foreach (Position pos in openPositions)
                    {
                        if (pos.Type == RecommendationType.Day)
                            days = false;

                        if (pos.Type == RecommendationType.Hour)
                            hours = false;

                        if (pos.Type == RecommendationType.Minute)
                            minutes = false;
                    }
                    //opp = manager.Opportunities;
                    var toMonitor = manager.ToMonitor;

                    // colocado aqui para não ter o delay entre a recomendação e o manaPosition
                    await Task.Delay(20000, stoppingToken);

                    if (!days && !hours && !minutes) 
                        _logger.LogWarning($"#### #### #### #### #### #### ####\n\t#### Atingido numero maximo de posicoes em aberto ####\n\t#### #### #### #### #### #### ####\n"); 
                    else
                        opp = await _marketSvc.CheckOppotunitiesByKlines(currentMarket, days, hours, minutes);

                    // tira as que sairam mas o ideal seria monitorar
                    var leftD = from day in opp.Days
                               where !toMonitor.Any(x => x.Data.Symbol == day.Symbol)
                               select day;
                    opp.Days = leftD.ToList();

                    var leftH = from obj in opp.Hours
                                where !toMonitor.Any(x => x.Data.Symbol == obj.Symbol)
                                select obj;
                    opp.Hours = leftH.ToList();

                    var leftM = from obj in opp.Minutes
                                where !toMonitor.Any(x => x.Data.Symbol == obj.Symbol)
                                select obj;
                    opp.Minutes = leftM.ToList();
                }

            }
            catch (Exception ex)
            {
                _logger.LogError($"ERROR: {DateTime.Now}, metodo: ExecuteAsync(), message: {ex.Message}");
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
