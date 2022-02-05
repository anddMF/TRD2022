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
using Trade02.Models.CrossCutting;
using Trade02.Models.Trade;

namespace Trade02
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private static MarketService _marketSvc;
        private static PortfolioService _portfolioSvc;

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

                var acc = await _portfolioSvc.GetBalance();
                bool runner = true;
                bool debug = false;
                
                List<IBinanceTick> previousData = new List<IBinanceTick>();
                List<Position> openPositions = new List<Position>();
                int previousCounter = 0;

                // precisa de um get para retornar moedas já possuídas; posso alimentar isso com um valor no appsettings
                List<string> ownedSymbols = new List<string>() { "BTCUSDT", "ETHUSDT", "AXSUSDT", "DOWNUSDT", "UPUSDT" };

                List<IBinanceTick> response = await _marketSvc.GetTopPercentages(20, "USDT", 12, ownedSymbols);
                previousData = response;

                Console.WriteLine("----------------- Lista incial capturada ------------------");
                Console.WriteLine();

                while (runner)
                {
                    // a primeira ação desse while é rodar o motor de posições abertas para verificar se precisa fazer vendas
                    // rodar esse de 30 em 30 segundos

                    if(openPositions.Count > 0)
                    {
                        // verificar os dados da moeda, comparar o valor atual com o valor de quando comprou (que está na lista)
                        // tirar a porcentagem dessa diferença de valor, se for prejuízo de X%, executar a venda. 
                        // Se for lucro, atualizar propriedade do último valor mais alto e fazer essa comparação com esse novo valor
                        PortfolioResponse res = await _portfolioSvc.ManageOpenPositions(openPositions, previousData);
                        openPositions = res.OpenPositions;
                        previousData = res.MonitorData;
                    }

                    await Task.Delay(30000, stoppingToken);

                    if(openPositions.Count < 5)
                    {
                        Console.WriteLine("------- Monitoramento -------");
                        previousCounter++;

                        // toda essa responsabilidade de filtrar oportunidades, deve ficar em outra camada. Problema é a lista previousData
                        // que seria perdida em outra camada
                        response = await _marketSvc.MonitorTopPercentages(previousData);

                        List<IBinanceTick> oportunities = _marketSvc.CheckOportunities(response, previousData);

                        if (oportunities.Count > 1)
                        {
                            var executedOrder = await _marketSvc.ExecuteOrder(openPositions, ownedSymbols, oportunities, response, previousCounter, debug);
                            openPositions = executedOrder.Positions;
                            ownedSymbols = executedOrder.OwnedSymbols;
                        }
                        else
                        {
                            _logger.LogWarning($"SEM OPORTUNIDADES {DateTimeOffset.Now}");
                        }

                        // X minutos para renovar os dados base da previousData
                        if (previousCounter == 15)
                        {
                            previousCounter = 0;
                            response = await _marketSvc.GetTopPercentages(20, "USDT", 12, ownedSymbols);
                            previousData = response;
                        }
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
