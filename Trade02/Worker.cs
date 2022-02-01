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

                var testeOrder = await _marketSvc.PlaceOrder("MANAUSDT");
                // a cada X minutos, renovar os dados da lista previousData. Se por X minutos essas moedas não valorizaram 1%, renova a lista.
                List<IBinanceTick> previousData = new List<IBinanceTick>();
                int previousCounter = 0;

                // precisa de um get para retornar moedas já possuídas; posso alimentar isso com um valor no appsettings
                List<string> openPositions = new List<string>() { "BTC", "ETH", "AXS" };

                // primeiro load da previousData
                List<IBinanceTick> response = await _marketSvc.GetTopPercentages(10, "USDT", 10, openPositions);
                previousData = response;
                Console.WriteLine("----------------- List incial capturada ");
                Console.WriteLine();
                // fazer essa busca a cada 1 minutos e verificar se algumas moedas subiram mais de 1%, se sim, compra
                while (runner)
                {
                    // a primeira ação desse while é rodar o motor de posições abertas para verificar se precisa fazer vendas
                    // rodar esse de 30 em 30 segundos

                    await Task.Delay(30000, stoppingToken);
                    Console.WriteLine("------- Monitoramento -------");
                    previousCounter++;

                    // toda essa responsabilidade de filtrar oportunidades, deve ficar em outra camada. Problema é a lista previousData
                    // que seria perdida em outra camada
                    response = await _marketSvc.MonitorTopPercentages(previousData);

                    List<IBinanceTick> oportunities = CheckOportunities(response, previousData);
                    if(oportunities.Count > 1)
                    {
                        // motor de compra

                        for (int i = 0; i < oportunities.Count; i++)
                        {
                            var current = response.Find(x => x.Symbol == oportunities[i].Symbol);

                            var count = current.PriceChangePercent - oportunities[i].PriceChangePercent;
                            _logger.LogInformation($"COMPRA: {DateTimeOffset.Now}, moeda: {oportunities[i].Symbol}, current percentage: {current.PriceChangePercent}, percentage change in {previousCounter}: {count}, value: {oportunities[i].AskPrice}");
                        }
                    } else
                    {
                        _logger.LogWarning($"SEM OPORTUNIDADES {DateTimeOffset.Now}");
                    }

                    // 5 minutos para renovar os dados base da previousData
                    if(previousCounter == 10)
                    {
                        previousCounter = 0;
                        response = await _marketSvc.GetTopPercentages(10, "USDT", 11, openPositions);
                        previousData = response;
                    }
                }

            } catch(Exception ex)
            {
                _logger.LogError($"ERROR at: {DateTimeOffset.Now}, message: {ex.Message}");
                throw ex;
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
                await Task.Delay(1000, stoppingToken);
            }
        }

        /// <summary>
        /// Cruza as listas de dados atuais das moedas e os anteriormente validados, verifica se existe uma valorização de X% para identificar uma tendencia de subida
        /// e, por consequência, uma possível compra. Retorna a lista de moedas que atendam a estes requisitos.
        /// </summary>
        /// <param name="currentData"></param>
        /// <param name="previousData"></param>
        /// <returns>Lista com as oportunidades de possíveis compras</returns>
        public static List<IBinanceTick> CheckOportunities(List<IBinanceTick> currentData, List<IBinanceTick> previousData)
        {
            List<IBinanceTick> result = new List<IBinanceTick>();

            var res = from obj in currentData
                      join prev in previousData on obj.Symbol equals prev.Symbol
                      where obj.PriceChangePercent - prev.PriceChangePercent > 1
                      select prev;

            result = res.ToList();

            return result;
        }
    }
}
