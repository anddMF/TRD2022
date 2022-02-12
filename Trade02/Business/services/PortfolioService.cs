﻿using Binance.Net.Interfaces;
using Binance.Net.Objects.Spot.MarketData;
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
        private readonly int maxPositionMinutes = AppSettings.TradeConfiguration.MaxPositionMinutes;
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

                    decimal currentValorization = ((marketPosition.AskPrice - currentPosition.LastMaxPrice) / currentPosition.LastMaxPrice) * 100;
                    decimal totalValorization = ((marketPosition.AskPrice - currentPosition.InitialPrice) / currentPosition.InitialPrice) * 100;

                    Console.WriteLine($"-------##### POSICAO> {currentPosition.Data.Symbol}, val: {totalValorization}, cur val: {currentValorization}, wei: {currentPosition.Data.WeightedAveragePrice}");

                    openPositions[i].LastMaxPrice = Math.Max(marketPosition.AskPrice, currentPosition.LastMaxPrice);

                    // não ficar muito tempo com uma moeda andando de lado na carteira
                    if (maxPositionMinutes > 0 && openPositions[i].Minutes >= maxPositionMinutes && totalValorization < 1)
                    {
                        Console.WriteLine("Caiu validacao do tempo");
                        var order = await _marketSvc.PlaceSellOrder(currentPosition.Data.Symbol, currentPosition.Quantity);

                        if (order != null)
                        {
                            openPositions[i].LastPrice = order.Price;
                            openPositions[i].LastValue = order.Price * openPositions[i].Quantity;
                            openPositions[i].Valorization = ((order.Price - currentPosition.InitialPrice) / currentPosition.InitialPrice) * 100;

                            _logger.LogInformation($"VENDA: {DateTime.Now}, moeda: {openPositions[i].Data.Symbol}, total valorization: {openPositions[i].Valorization}, current price: {marketPosition.AskPrice}, initial: {openPositions[i].InitialPrice}");

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
                        // colocar uma validacao teste pra se já tiver 1% de lucro, diminuir esse numero negativo para a venda, assim realiza pelo menos o 1%
                        // se eu acabei de vim de uma subida de masi de 1% e meu lucro esta acima de 1%, low level >= 0
                        decimal lowLevel = totalValorization >= (decimal)1 ? (decimal)-0.1 : (decimal)-0.3;
                        if (totalValorization <= (decimal)-0.2)
                        {
                            var order = await _marketSvc.PlaceSellOrder(currentPosition.Data.Symbol, currentPosition.Quantity);

                            if (order != null)
                            {
                                // retorna a moeda para o previous para continuar acompanhando caso seja uma queda de mercado
                                //previousData.Add(marketPosition);

                                openPositions[i].LastPrice = order.Price;
                                openPositions[i].LastValue = order.Price * openPositions[i].Quantity;
                                openPositions[i].Valorization = ((order.Price - currentPosition.InitialPrice) / currentPosition.InitialPrice) * 100;

                                _logger.LogInformation($"VENDA: {DateTime.Now}, moeda: {openPositions[i].Data.Symbol}, total valorization: {openPositions[i].Valorization}, current price: {marketPosition.AskPrice}, initial: {openPositions[i].InitialPrice}");

                                ReportLog.WriteReport(logType.VENDA, openPositions[i]);
                            }

                        }
                        else if (currentValorization <= lowLevel)
                        {
                            Console.WriteLine("\n----- venda low level");
                            var order = await _marketSvc.PlaceSellOrder(currentPosition.Data.Symbol, currentPosition.Quantity);

                            if (order != null)
                            {
                                // retorna a moeda para o previous para continuar acompanhando caso seja uma queda de mercado
                                if (totalValorization > 0)
                                    previousData.Add(marketPosition);

                                openPositions[i].LastPrice = order.Price;
                                openPositions[i].LastValue = order.Price * openPositions[i].Quantity;
                                openPositions[i].Valorization = ((order.Price - currentPosition.InitialPrice) / currentPosition.InitialPrice) * 100;

                                _logger.LogWarning($"VENDA: {DateTime.Now}, moeda: {openPositions[i].Data.Symbol}, total valorization: {openPositions[i].Valorization}, current price: {marketPosition.AskPrice}, initial: {openPositions[i].InitialPrice}");

                                ReportLog.WriteReport(logType.VENDA, openPositions[i]);
                            }
                        }
                        else
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

        public async Task<BinanceOrderBook> GetOrderBook(string symbol, int limit)
        {
            try
            {
                var res = await _clientSvc.GetOrderBook(symbol, limit);

                return res;

            }
            catch (Exception ex)
            {
                _logger.LogError($"ERROR: {DateTime.Now}, metodo: PortfolioService.ManageOpenPositions(), message: {ex.Message}");
                return null;
            }
        }

        public async Task<BinancePlacedOrder> ExecuteSellOrder(string symbol, decimal quantity)
        {
            Console.WriteLine("#### Entrou VENDA");
            decimal prevPrice = 0;
            int j = 0;

            while (j < 5)
            {
                await Task.Delay(3000);
                var market = await _clientSvc.GetTicker(symbol);
                decimal price = market.AskPrice;
                Console.WriteLine($"Preco {symbol}: {price}");

                if (j > 0 && price > prevPrice)
                {
                    var order = await _marketSvc.PlaceSellOrder(symbol, quantity);
                    if (order == null)
                        _logger.LogWarning($"#### #### #### #### #### #### ####\n\t### VENDA de {symbol} NAO EXECUTADA ###\n\t#### #### #### #### #### #### ####");
                    else
                        return order;
                }

                prevPrice = price;
                j++;
            }

            var final = await _marketSvc.PlaceSellOrder(symbol, quantity);
            if (final == null)
                _logger.LogWarning($"#### #### #### #### #### #### ####\n\t### VENDA de {symbol} NAO EXECUTADA ###\n\t#### #### #### #### #### #### ####");
            else
                return final;

            return null;

        }
        public async Task<bool> ManagePosition(OpportunitiesResponse opp, List<Position> positions)
        {
            // manage das posicoes em aberto
            for(int i = 0; i < positions.Count; i++)
            {
                var market = await _clientSvc.GetTicker(positions[i].Data.Symbol);
                decimal currentPrice = market.AskPrice;

                decimal currentValorization = currentPrice - positions[i].LastPrice;

                positions[i].LastPrice = currentPrice;
                // pego o preço atual da moeda
                // currentValorization comparado com o lastTotalValorization
                // if lastTotalValorization > 1 then currentValorization nao pode ser < x
                if (currentValorization < 0)
                {
                    if (positions[i].Valorization >= 1)
                    {
                        if (currentValorization <= (decimal)0.4)
                        {
                            // executeSellOrder
                            var order = await ExecuteSellOrder(positions[i].Data.Symbol, positions[i].Quantity);
                            _logger.LogWarning($"VENDA: {DateTime.Now}, moeda: {positions[i].Data.Symbol}, total valorization: {positions[i].Valorization}, current price: {order.Price}, initial: {positions[i].InitialPrice}");
                            // jogar para um  novo objeto que sera usado para monitorar essa posicao caso volte a subir
                            //position = new Position(market, order.Price, order.Quantity);

                            ReportLog.WriteReport(logType.VENDA, positions[i]);
                        }
                    } else if (currentValorization + positions[i].Valorization <= positions[i].Risk)
                    {
                        // executeSellOrder
                        var order = await ExecuteSellOrder(positions[i].Data.Symbol, positions[i].Quantity);
                        _logger.LogWarning($"VENDA: {DateTime.Now}, moeda: {positions[i].Data.Symbol}, total valorization: {positions[i].Valorization}, current price: {order.Price}, initial: {positions[i].InitialPrice}");
                        // jogar para um  novo objeto que sera usado para monitorar essa posicao caso volte a subir
                        //position = new Position(market, order.Price, order.Quantity);

                        ReportLog.WriteReport(logType.VENDA, positions[i]);
                    }

                } else
                {
                    // se essa moeda tiver renovando maximas acima de 0.7%, ficar nela até parar de renovar para poder pegar um lucro bom. depois vende
                    // while
                }

                positions[i].Valorization = ((currentPrice - positions[i].InitialPrice) / positions[i].InitialPrice) * 100;
            }

            // se ainda esta nas listas de opp, quer dizer que não foram compradas 
            if (opp.Minutes.Count > 0)
            {
                // executar a compra. Por enquanto é só uma recomendação de cada então não precisa de loop
                var res = await ExecuteSimpleOrder(opp.Minutes[0].Symbol);
                if (res != null)
                {
                    res.Risk = -3;
                    positions.Add(res);
                    opp.Minutes.Clear();
                }
            }

            if (opp.Days.Count > 0)
            {
                // executar a compra. Por enquanto é só uma recomendação de cada então não precisa de loop
                var res = await ExecuteSimpleOrder(opp.Days[0].Symbol);
                if(res != null)
                {
                    res.Risk = -7;
                    positions.Add(res);
                    opp.Days.Clear();
                }
            } 

            if (opp.Hours.Count > 0)
            {
                // executar a compra. Por enquanto é só uma recomendação de cada então não precisa de loop
                var res = await ExecuteSimpleOrder(opp.Hours[0].Symbol);
                if(res != null)
                {
                    res.Risk = -11;
                    positions.Add(res);
                    opp.Hours.Clear();
                }
            }
        }

        /// <summary>
        /// Executa as ordens de compras que cumpram as condições necessárias para tal.
        /// </summary>
        /// <returns></returns>
        public async Task<OrderResponse> ExecuteMulitpleOrder(List<string> symbols)
        {
            // falta um controle para recomendações repetidas
            decimal quantity = await GetUSDTAmount();
            List<Position> openPositions = new List<Position>();
            List<string> notBought = new List<string>();
            List<string> bought = new List<string>();
            if (quantity == 0)
                return null;

            // rodar X vezes no loop e ver se o preço está só baixando, se ele somente cair não é um bom sinal. Se não conseguir fazer a compra por estar somente caindo, cancela essa recomendação.
            decimal prevPrice = 0;
            for (int i = 0; i < symbols.Count; i++)
            {
                string symbol = symbols[i];
                prevPrice = 0;
                int j = 0;
                while (j < 5)
                {
                    var market = await _clientSvc.GetTicker(symbol);
                    decimal price = market.AskPrice;

                    if (j > 0 && price > prevPrice)
                    {
                        var order = await _marketSvc.PlaceBuyOrder(symbol, quantity);
                        if (order == null)
                        {
                            // não executou, eu faço log do problema na tela mas ainda tenho que ver os possíveis erros pra saber como tratar
                            _logger.LogWarning($"#### #### #### #### #### #### ####\n\t### Compra de {symbol} NAO EXECUTADA ###\n\t#### #### #### #### #### #### ####");
                        }
                        else
                        {
                            _logger.LogInformation($"COMPRA: {DateTime.Now}, moeda: {symbol}, current percentage: {market.PriceChangePercent}, price: {order.Price}");
                            bought.Add(symbols[i]);

                            Position position = new Position(market, order.Price, order.Quantity);
                            openPositions.Add(position);

                            ReportLog.WriteReport(logType.COMPRA, position);
                            j = 10;
                        }
                    }

                    prevPrice = price;
                    j++;
                }
            }

            if (bought.Count != symbols.Count)
            {
                var left = from sym in symbols
                           where !bought.Any(x => x == sym)
                           select sym;

                notBought = left.ToList();
            }

            return new OrderResponse(openPositions, bought, notBought);
        }

        /// <summary>
        /// Executa uma ordem de compra que cumpra as condições necessárias para tal.
        /// </summary>
        /// <returns></returns>
        public async Task<Position> ExecuteSimpleOrder(string symbol)
        {
            decimal quantity = await GetUSDTAmount();
            if (quantity == 0)
                return null;

            Position position = new Position();

            decimal prevPrice = 0;
            int j = 0;

            while (j < 5)
            {
                var market = await _clientSvc.GetTicker(symbol);
                decimal price = market.AskPrice;

                if (j > 0 && price > prevPrice)
                {
                    var order = await _marketSvc.PlaceBuyOrder(symbol, quantity);
                    if (order == null)
                    {
                        // não executou, eu faço log do problema na tela mas ainda tenho que ver os possíveis erros pra saber como tratar
                        _logger.LogWarning($"#### #### #### #### #### #### ####\n\t### Compra de {symbol} NAO EXECUTADA ###\n\t#### #### #### #### #### #### ####");
                    }
                    else
                    {
                        _logger.LogInformation($"COMPRA: {DateTime.Now}, moeda: {symbol}, current percentage: {market.PriceChangePercent}, price: {order.Price}");
                        position = new Position(market, order.Price, order.Quantity);

                        ReportLog.WriteReport(logType.COMPRA, position);
                        j = 10;
                    }
                }

                prevPrice = price;
                j++;
            }

            return position.Data != null ? position : null;
        }

        /// <summary>
        /// Get the amount of USDT that can be spent on an order.
        /// </summary>
        /// <returns></returns>
        public async Task<decimal> GetUSDTAmount()
        {
            var balance = await GetBalance("USDT");

            if (balance == null)
                return 0;

            decimal totalUsdt = balance.Total;

            // teto de gastos
            totalUsdt = Math.Min(totalUsdt, maxBuyAmount);

            // formula para se fazer compras de no minimo 15 usdt
            decimal quantity = totalUsdt / maxOpenPositions;
            decimal support = totalUsdt / minUSDT;
            decimal supportQuantity = totalUsdt / support;

            if (quantity < minUSDT && supportQuantity < minUSDT)
            {
                _logger.LogWarning($"#### #### #### #### #### #### ####\n\t#### SALDO USDT INSUFICIENTE PARA COMPRAS ####\n\t#### #### #### #### #### #### ####");

                return 0;
            }

            return Math.Max(quantity, supportQuantity);
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

        /// <summary>
        /// Executa as ordens de compras que cumpram as condições necessárias para tal.
        /// </summary>
        /// <param name="openPositions">posições em aberto pelo robô</param>
        /// <param name="symbolsOwned"></param>
        /// <param name="opportunities">dados previous de moedas que valorizaram positivamente</param>
        /// <param name="currentMarket">dados atuais das moedas em monitoramento</param>
        /// <param name="minute"></param>
        /// <returns></returns>
        /*
        public async Task<OrderResponse> ExecuteOrder(List<Position> openPositions, List<string> symbolsOwned, List<IBinanceTick> opportunities, List<IBinanceTick> currentMarket, List<IBinanceTick> previousData, int minute)
        {
            // não comprar de primeira, registrar o preço quando estrar e fazer mais duas rodadas pra ver se ele baixa
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

            for (int i = 0; i < opportunities.Count; i++)
            {
                if (openPositions.Count < maxOpenPositions)
                {
                    var current = currentMarket.Find(x => x.Symbol == opportunities[i].Symbol);

                    var percentageChange = current.PriceChangePercent - opportunities[i].PriceChangePercent;
                    _logger.LogInformation($"COMPRA: {DateTime.Now}, moeda: {opportunities[i].Symbol}, current percentage: {current.PriceChangePercent}, percentage change in {minute}: {percentageChange}, price: {opportunities[i].AskPrice}");

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
                        previousData.RemoveAll(x => x.Symbol == opportunities[i].Symbol);

                        ReportLog.WriteReport(logType.COMPRA, position);
                    }
                }
                else
                {
                    return new OrderResponse(openPositions, symbolsOwned, previousData);
                }

            }

            return new OrderResponse(openPositions, symbolsOwned, previousData);
        } */
    }
}
