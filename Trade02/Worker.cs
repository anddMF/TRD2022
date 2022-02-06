using Binance.Net;
using Binance.Net.Interfaces;
using Binance.Net.Objects;
using CryptoExchange.Net.Authentication;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Trade02.Business.services;
using Trade02.Infra.Cross;
using Trade02.Infra.DAL;
using Trade02.Infra.DAO;
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
                bool debug = false;

                List<IBinanceTick> previousData = new List<IBinanceTick>();
                List<Position> openPositions = new List<Position>();
                int minutesCounter = 0;

                List<string> ownedSymbols = AppSettings.TradeConfiguration.OwnedSymbols;

                List<IBinanceTick> response = await _marketSvc.GetTopPercentages(maxToMonitor, currency, maxSearchPercentage, ownedSymbols);
                previousData = response;

                Console.WriteLine("----------------- Lista incial capturada ------------------");
                Console.WriteLine();

                while (runner)
                {
                    await Task.Delay(20000, stoppingToken);

                    // Manipula as operacoes em aberto
                    if (openPositions.Count > 0)
                    {
                        PortfolioResponse res = await _portfolioSvc.ManageOpenPositions(openPositions, previousData);
                        openPositions = res.OpenPositions;
                        previousData = res.MonitorData;
                    }


                    if (openPositions.Count < maxOpenPositions)
                    {
                        Console.WriteLine("----------------- Monitoramento ------------------");
                        minutesCounter++;

                        // toda essa responsabilidade de filtrar oportunidades, deve ficar em outra camada. Problema é a lista previousData
                        // que seria perdida em outra camada
                        response = await _marketSvc.MonitorTopPercentages(previousData);

                        List<IBinanceTick> oportunities = _marketSvc.CheckOportunities(response, previousData);

                        if (oportunities.Count > 1)
                        {
                            var executedOrder = await _portfolioSvc.ExecuteOrder(openPositions, ownedSymbols, oportunities, response, minutesCounter, debug);

                            if (executedOrder != null)
                            {
                                openPositions = executedOrder.Positions;
                                ownedSymbols = executedOrder.OwnedSymbols;
                            }
                        }
                        else
                        {
                            _logger.LogWarning($"SEM OPORTUNIDADES {DateTime.Now}");
                        }

                        // X minutos para renovar os dados base da previousData
                        if (minutesCounter == 30)
                        {
                            minutesCounter = 0;
                            response = await _marketSvc.GetTopPercentages(maxToMonitor, currency, maxSearchPercentage, ownedSymbols);
                            Console.WriteLine("----------------- Renovada lista de monitoramento");

                            previousData = response;
                        }
                    }
                    else
                    {
                        _logger.LogWarning($"#### #### #### #### #### #### ####\n\t#### Atingido numero maximo de posicoes em aberto ####\n\t#### #### #### #### #### #### ####");
                        response = await _marketSvc.GetTopPercentages(maxToMonitor, currency, maxSearchPercentage, ownedSymbols);

                        previousData = response;
                    }

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
