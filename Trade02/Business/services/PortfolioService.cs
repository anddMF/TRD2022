﻿using Binance.Net.Interfaces;
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

        public PortfolioService(IHttpClientFactory clientFactory, ILogger<Worker> logger)
        {
            _logger = logger;
            _clientSvc = new APICommunication(clientFactory);
            _marketSvc = new MarketService(clientFactory, logger);
        }
        
        public async Task<PortfolioResponse> ManageOpenPositions(List<Position> openPositions, List<IBinanceTick> previousData)
        {
            List<Position> result = new List<Position>();
            try
            {
                for (int i = 0; i < openPositions.Count; i++)
                {
                    Position currentPosition = openPositions[i];
                    var marketPosition = await _marketSvc.GetSingleTicker(currentPosition.Data.Symbol);

                    decimal currentValorization = ((marketPosition.AskPrice - currentPosition.CurrentPrice) / currentPosition.CurrentPrice) * 100;
                    decimal totalValorization = ((marketPosition.AskPrice - currentPosition.InitialPrice) / currentPosition.InitialPrice) * 100;

                    // Valida a variação da moeda para vender se enxergar uma tendencia de queda (a partir do preço atual) ou uma valorização TOTAL negativa da moeda.
                    // Caso tenha uma valorização positiva, atualiza os dados no open position e não executa a venda
                    if (currentValorization > 0)
                    {
                        // entra na area de lucro ATUAL, não o total

                        openPositions[i].CurrentPrice = marketPosition.AskPrice;
                        openPositions[i].LastPrice = marketPosition.AskPrice;
                        openPositions[i].LastValue = marketPosition.AskPrice * openPositions[i].Quantity;
                        openPositions[i].Valorization += currentValorization;
                        result.Add(currentPosition);
                    }
                    else
                    {
                        if (totalValorization < (decimal)-1.4)
                        {
                            // venda porque já ta no prejuízo

                            var order = await _marketSvc.PlaceSellOrder(currentPosition.Data.Symbol, currentPosition.LastValue);
                            
                            if (order != null)
                            {
                                // retorna a moeda para o previous para continuar acompanhando caso seja uma queda de mercado
                                previousData.Add(marketPosition);

                                openPositions[i].CurrentPrice = order.Price;
                                openPositions[i].LastPrice = order.Price;
                                openPositions[i].LastValue = order.Price * openPositions[i].Quantity;
                                openPositions[i].Valorization += totalValorization;

                                ReportLog.WriteReport(logType.VENDA, openPositions[i]);
                            }

                            break;
                        }
                        if (currentValorization < (decimal)-1.4)
                        {
                            // venda porque pode ser a tendencia de queda

                            var order = await _marketSvc.PlaceSellOrder(currentPosition.Data.Symbol, currentPosition.LastValue);
                            
                            if(order != null)
                            {
                                // retorna a moeda para o previous para continuar acompanhando caso seja uma queda de mercado
                                previousData.Add(marketPosition);

                                openPositions[i].CurrentPrice = order.Price;
                                openPositions[i].LastPrice = order.Price;
                                openPositions[i].LastValue = order.Price * openPositions[i].Quantity;
                                openPositions[i].Valorization += totalValorization;

                                ReportLog.WriteReport(logType.VENDA, openPositions[i]);
                            }
                            break;
                        }
                        result.Add(currentPosition);
                    }
                }

                return new PortfolioResponse(previousData, result);

            }
            catch (Exception ex)
            {
                _logger.LogError($"ERROR: {DateTimeOffset.Now}, metodo: PortfolioService.ManageOpenPositions(), message: {ex.Message}");
                return null;
            }
        }

        public async Task<OrderResponse> ExecuteOrder(List<Position> openPositions, List<string> symbolsOwned, List<IBinanceTick> oportunities, List<IBinanceTick> response, int minute, bool debug = false)
        {
            var balance = await GetBalance("USDT");
            decimal totalUsdt = balance.Total;

            // formula para se fazer compras de no minimo 14 usdt
            decimal quantity = totalUsdt / (5 - openPositions.Count);
            decimal support = totalUsdt / 14;
            decimal supportQuantity = totalUsdt / support;

            if (quantity < 14 && supportQuantity < 14)
            {
                _logger.LogWarning($"#### #### #### #### #### #### ####");
                _logger.LogWarning($"#### SALDO USDT INSUFICIENTE PARA COMPRAS ####");
                _logger.LogWarning($"#### Posicoes em aberto: {openPositions.Count} ####");
                _logger.LogWarning($"#### #### #### #### #### #### ####");

                return null;
            }

            quantity = Math.Max(quantity, supportQuantity);

            for (int i = 0; i < oportunities.Count; i++)
            {
                var current = response.Find(x => x.Symbol == oportunities[i].Symbol);

                var count = current.PriceChangePercent - oportunities[i].PriceChangePercent;
                _logger.LogInformation($"COMPRA: {DateTimeOffset.Now}, moeda: {oportunities[i].Symbol}, current percentage: {current.PriceChangePercent}, percentage change in {minute}: {count}, value: {oportunities[i].AskPrice}");

                if (!debug)
                {
                    // controle de numero maximo de posicoes em aberto
                    if (openPositions.Count < 5)
                    {
                        // executa a compra
                        var order = await _marketSvc.PlaceBuyOrder(current.Symbol, quantity);
                        if (order == null)
                        {
                            // não executou, eu faço log do problema na tela mas ainda tenho que ver os possíveis erros pra saber como tratar
                            _logger.LogWarning($"#### #### #### #### #### #### ####");
                            _logger.LogWarning($"### Compra de {current.Symbol} NAO EXECUTADA ###");
                            _logger.LogWarning($"#### #### #### #### #### #### ####");
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
                _logger.LogError($"ERROR: {DateTimeOffset.Now}, metodo: PortfolioService.GetBalance(), message: {ex.Message}");
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
                _logger.LogError($"ERROR: {DateTimeOffset.Now}, metodo: PortfolioService.GetBalance(), message: {ex.Message}");
                return null;
            }

        }
    }
}
