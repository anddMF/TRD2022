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
using Trade02.Infra.DAL;
using Trade02.Infra.DAO;
using Trade02.Models.Trade;

namespace Trade02
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private static MarketService _marketSvc;

        public Worker(ILogger<Worker> logger, IHttpClientFactory clientFactory)
        {
            _logger = logger;
            _marketSvc = new MarketService(clientFactory, logger);
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
                int previousCounter = 0;

                // precisa de um get para retornar moedas já possuídas; posso alimentar isso com um valor no appsettings
                List<string> symbolsOwned = new List<string>() { "BTCUSDT", "ETHUSDT", "AXSUSDT", "DOWNUSDT", "UPUSDT" };

                List<IBinanceTick> response = await _marketSvc.GetTopPercentages(20, "USDT", 12, symbolsOwned);
                previousData = response;

                Console.WriteLine("----------------- Lista incial capturada ------------------");
                Console.WriteLine();

                // fazer essa busca a cada 1 minuto e verificar se algumas moedas subiram mais de 1%, se sim, recomenda compra
                while (runner)
                {
                    // a primeira ação desse while é rodar o motor de posições abertas para verificar se precisa fazer vendas
                    // rodar esse de 30 em 30 segundos

                    await Task.Delay(60000, stoppingToken);

                    Console.WriteLine("------- Monitoramento -------");
                    previousCounter++;

                    // toda essa responsabilidade de filtrar oportunidades, deve ficar em outra camada. Problema é a lista previousData
                    // que seria perdida em outra camada
                    response = await _marketSvc.MonitorTopPercentages(previousData);

                    List<IBinanceTick> oportunities = _marketSvc.CheckOportunities(response, previousData);

                    if (oportunities.Count > 1)
                    {
                        var executedOrder = await _marketSvc.ExecuteOrder(openPositions, symbolsOwned, oportunities, response, previousCounter, debug);
                    }
                    else
                    {
                        _logger.LogWarning($"SEM OPORTUNIDADES {DateTimeOffset.Now}");
                    }

                    // X minutos para renovar os dados base da previousData
                    if (previousCounter == 15)
                    {
                        previousCounter = 0;
                        response = await _marketSvc.GetTopPercentages(20, "USDT", 12, symbolsOwned);
                        previousData = response;
                    }
                }

            }
            catch (Exception ex)
            {
                _logger.LogError($"ERROR: {DateTimeOffset.Now}, metodo: ExecuteAsync(), message: {ex.Message}");
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
