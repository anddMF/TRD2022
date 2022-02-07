using Binance.Net.Interfaces;
using Binance.Net.Objects.Spot.SpotData;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Trade02.Infra.Cross;
using Trade02.Infra.DAL;
using Trade02.Models.CrossCutting;
using Trade02.Models.Trade;
using static Trade02.Infra.Cross.ReportLog;

namespace Trade02.Business.services
{
    /// <summary>
    /// Responsável por manipular dados da carteira de investimentos.
    /// </summary>
    public class PortfolioService
    {
        private static APICommunication _clientSvc;
        private static MarketService _marketSvc;
        private readonly ILogger<Worker> _logger;

        private readonly int maxOpenPositions = AppSettings.TradeConfiguration.MaxOpenPositions;
        private readonly decimal maxBuyAmount = AppSettings.TradeConfiguration.MaxBuyAmount;
        private readonly int minUSDT = 15;

        public PortfolioService(IHttpClientFactory clientFactory, ILogger<Worker> logger)
        {
            _logger = logger;
            _clientSvc = new APICommunication(clientFactory);
            _marketSvc = new MarketService(clientFactory, logger);
        }

        /// <summary>
        /// Motor de manipulação das posições em aberto. A partir de certas condições, determina o sell ou hold da posição.
        /// </summary>
        /// <param name="openPositions">posições em aberto</param>
        /// <param name="previousData">dados que estão sendo monitorados, usado somente para Add de moedas a serem monitoradas</param>
        /// <returns>lista de posições ainda em aberto</returns>
        public async Task<PortfolioResponse> ManageOpenPositions(List<Position> openPositions, List<IBinanceTick> previousData)
        {
            // lista que será retornada com as posições que foram mantidas em aberto
            List<Position> result = new List<Position>();
            try
            {
                // SE O TICKER DE MINUTO FOR X E O TOTALVALORIZATION < 1, VENDE
                Console.WriteLine($"----###### MANAGE: posicoes {openPositions.Count}");
                for (int i = 0; i < openPositions.Count; i++)
                {
                    Position currentPosition = openPositions[i];
                    var marketPosition = await _marketSvc.GetSingleTicker(currentPosition.Data.Symbol);
                    // -0.01 adicionado para o preço de venda ter menos chance de não ser executado
                    openPositions[i].LastValue = (marketPosition.AskPrice * openPositions[i].Quantity) - (decimal)0.01;

                    openPositions[i].Minutes++;

                    decimal currentValorization = ((marketPosition.AskPrice - currentPosition.CurrentPrice) / currentPosition.CurrentPrice) * 100;
                    decimal totalValorization = ((marketPosition.AskPrice - currentPosition.InitialPrice) / currentPosition.InitialPrice) * 100;

                    Console.WriteLine($"-------##### POSICAO> {currentPosition.Data.Symbol}, {openPositions[i].Minutes}");

                    openPositions[i].CurrentPrice = marketPosition.AskPrice;

                    // não ficar muito tempo com uma moeda andando de lado na carteira
                    if(openPositions[i].Minutes >= 33 && totalValorization < 1)
                    {
                        Console.WriteLine("Caiu validacao do tempo");
                        var order = await _marketSvc.PlaceSellOrder(currentPosition.Data.Symbol, currentPosition.Quantity);

                        if (order != null)
                        {
                            openPositions[i].CurrentPrice = order.Price;
                            openPositions[i].LastPrice = order.Price;
                            openPositions[i].LastValue = order.Price * openPositions[i].Quantity;
                            openPositions[i].Valorization = ((order.Price - currentPosition.InitialPrice) / currentPosition.InitialPrice) * 100;

                            _logger.LogInformation($"VENDA: {DateTime.Now}, moeda: {openPositions[i].Data.Symbol}, total valorization: {openPositions[i].Valorization}, current price: {openPositions[i].CurrentPrice}, initial: {openPositions[i].InitialPrice}");

                            ReportLog.WriteReport(logType.VENDA, openPositions[i]);
                        }
                    }
                    // Valida a variação da moeda para vender se enxergar uma tendencia de queda (a partir do preço atual) ou uma valorização TOTAL negativa da moeda.
                    // Caso tenha uma valorização positiva, atualiza os dados no open position e não executa a venda
                    else if (currentValorization > 0)
                    {
                        //Console.WriteLine($"\n############## Manage: ticker {openPositions[i].Data.Symbol}, valorizado em {totalValorization}, current {currentValorization}");

                        openPositions[i].LastPrice = marketPosition.AskPrice;
                        openPositions[i].LastValue = marketPosition.AskPrice * openPositions[i].Quantity;
                        openPositions[i].Valorization = totalValorization;
                        result.Add(currentPosition);
                    }
                    else
                    {
                        if (totalValorization <= (decimal)-1.2)
                        {
                            var order = await _marketSvc.PlaceSellOrder(currentPosition.Data.Symbol, currentPosition.Quantity);

                            if (order != null)
                            {
                                // retorna a moeda para o previous para continuar acompanhando caso seja uma queda de mercado
                                previousData.Add(marketPosition);

                                openPositions[i].CurrentPrice = order.Price;
                                openPositions[i].LastPrice = order.Price;
                                openPositions[i].LastValue = order.Price * openPositions[i].Quantity;
                                openPositions[i].Valorization = ((order.Price - currentPosition.InitialPrice) / currentPosition.InitialPrice) * 100;

                                _logger.LogInformation($"VENDA: {DateTime.Now}, moeda: {openPositions[i].Data.Symbol}, total valorization: {openPositions[i].Valorization}, current price: {openPositions[i].CurrentPrice}, initial: {openPositions[i].InitialPrice}");

                                ReportLog.WriteReport(logType.VENDA, openPositions[i]);
                            }

                        }
                        else if (currentValorization <= (decimal)-1.4)
                        {
                            var order = await _marketSvc.PlaceSellOrder(currentPosition.Data.Symbol, currentPosition.Quantity);

                            if (order != null)
                            {
                                // retorna a moeda para o previous para continuar acompanhando caso seja uma queda de mercado
                                previousData.Add(marketPosition);

                                openPositions[i].CurrentPrice = order.Price;
                                openPositions[i].LastPrice = order.Price;
                                openPositions[i].LastValue = order.Price * openPositions[i].Quantity;
                                openPositions[i].Valorization = ((order.Price - currentPosition.InitialPrice) / currentPosition.InitialPrice) * 100;

                                _logger.LogWarning($"VENDA: {DateTime.Now}, moeda: {openPositions[i].Data.Symbol}, total valorization: {openPositions[i].Valorization}, current price: {openPositions[i].CurrentPrice}, initial: {openPositions[i].InitialPrice}");

                                ReportLog.WriteReport(logType.VENDA, openPositions[i]);
                            }
                            break;
                        } else
                        {
                            result.Add(currentPosition);
                        }
                    }
                }

                return new PortfolioResponse(previousData, result);
            }
            catch (Exception ex)
            {
                _logger.LogError($"ERROR: {DateTime.Now}, metodo: PortfolioService.ManageOpenPositions(), message: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Executa as ordens de compras que cumpram as condições necessárias para tal.
        /// </summary>
        /// <param name="openPositions">posições em aberto pelo robô</param>
        /// <param name="symbolsOwned"></param>
        /// <param name="oportunities">dados previous de moedas que valorizaram positivamente</param>
        /// <param name="currentMarket">dados atuais das moedas em monitoramento</param>
        /// <param name="minute"></param>
        /// <returns></returns>
        public async Task<OrderResponse> ExecuteOrder(List<Position> openPositions, List<string> symbolsOwned, List<IBinanceTick> oportunities, List<IBinanceTick> currentMarket, int minute)
        {
            var balance = await GetBalance("USDT");
            decimal totalUsdt = balance.Total;

            // teto de gastos
            totalUsdt = Math.Min(totalUsdt, maxBuyAmount);

            // formula para se fazer compras de no minimo 15 usdt
            decimal quantity = totalUsdt / maxOpenPositions;
            decimal support = totalUsdt / minUSDT;
            decimal supportQuantity = totalUsdt / support;

            if (quantity < minUSDT && supportQuantity < minUSDT)
            {
                _logger.LogWarning($"#### #### #### #### #### #### ####\n\t#### SALDO USDT INSUFICIENTE PARA COMPRAS ####\n\t#### Posicoes em aberto: {openPositions.Count} ####\n\t#### #### #### #### #### #### ####");

                return null;
            }

            quantity = Math.Max(quantity, supportQuantity);

            for (int i = 0; i < oportunities.Count; i++)
            {
                if (openPositions.Count < maxOpenPositions)
                {
                    var current = currentMarket.Find(x => x.Symbol == oportunities[i].Symbol);

                    var percentageChange = current.PriceChangePercent - oportunities[i].PriceChangePercent;
                    _logger.LogInformation($"COMPRA: {DateTime.Now}, moeda: {oportunities[i].Symbol}, current percentage: {current.PriceChangePercent}, percentage change in {minute}: {percentageChange}, price: {oportunities[i].AskPrice}");

                    // executa a compra
                    var order = await _marketSvc.PlaceBuyOrder(current.Symbol, quantity);
                    if (order == null)
                    {
                        // não executou, eu faço log do problema na tela mas ainda tenho que ver os possíveis erros pra saber como tratar
                        _logger.LogWarning($"#### #### #### #### #### #### ####\n\t### Compra de {current.Symbol} NAO EXECUTADA ###\n\t#### #### #### #### #### #### ####");
                    }
                    else
                    {
                        // adicionar mais validações pois o quantity pode não ter sido 100% filled

                        symbolsOwned.Add(current.Symbol);
                        Position position = new Position(current, order.Price, order.Quantity);
                        openPositions.Add(position);

                        ReportLog.WriteReport(logType.COMPRA, position);
                    }
                }
                else
                {
                    return new OrderResponse(openPositions, symbolsOwned);
                }

            }

            return new OrderResponse(openPositions, symbolsOwned);
        }

        /// <summary>
        /// Get dos balanços das moedas em carteira.
        /// </summary>
        /// <returns></returns>
        public async Task<List<BinanceBalance>> GetBalance()
        {
            try
            {
                var response = await _clientSvc.GetAccountInfo();

                return response.Balances.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError($"ERROR: {DateTime.Now}, metodo: PortfolioService.GetBalance(), message: {ex.Message}");
                return null;
            }

        }

        /// <summary>
        /// Get do balanço de uma moeda em carteira.
        /// </summary>
        /// <param name="symbol"></param>
        /// <returns></returns>
        public async Task<BinanceBalance> GetBalance(string symbol)
        {
            try
            {
                var response = await _clientSvc.GetAccountInfo();

                return response.Balances.ToList().Find(x => x.Asset == symbol);
            }
            catch (Exception ex)
            {
                _logger.LogError($"ERROR: {DateTime.Now}, metodo: PortfolioService.GetBalance(), message: {ex.Message}");
                return null;
            }

        }
    }
}
