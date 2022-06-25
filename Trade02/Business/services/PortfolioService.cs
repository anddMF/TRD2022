using Binance.Net.Interfaces;
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
    public class PortfolioService : IPortfolioService
    {
        #region setup variables
        private static APICommunication _clientSvc;
        private static MarketService _marketSvc;
        private static IEventsOutput _eventsOutput;
        private readonly ILogger _logger;

        private readonly int maxOpenPositions = AppSettings.TradeConfiguration.MaxOpenPositions;
        private readonly decimal maxBuyAmount = AppSettings.TradeConfiguration.MaxBuyAmount;
        private readonly int minUSDT = 15;

        private int openDayPositions = 0;
        private int openHourPositions = 0;
        private int openMinutePositions = 0;
        #endregion

        public PortfolioService(IHttpClientFactory clientFactory, ILogger<PortfolioService> logger, IEventsOutput eventsOutput)
        {
            _logger = logger;
            _eventsOutput = eventsOutput;
            _clientSvc = new APICommunication(clientFactory);
            _marketSvc = new MarketService(clientFactory, logger);
            _logger.LogInformation("teste");
            MaxPositionsPerType();
        }

        /// <summary>
        /// Motor de manipulação das posições em aberto e recomendadas. A partir de certas condições, determina o sell ou hold da posição.
        /// </summary>
        /// <param name="opp">oportunidades de compra</param>
        /// <param name="positions">posições que já estão em aberto</param>
        /// <returns></returns>
        public async Task<ManagerResponse> ManagePosition(OpportunitiesResponse opp, List<Position> positions, List<Position> toMonitor)
        {
            HashSet<string> alreadyUsed = new HashSet<string>(positions.ConvertAll(x => x.Symbol).ToList());
            UpdateOpenPositionsPerType(positions);

            int stopCounter = 0;
            var sold = new List<string>();

            // se já tiver passado do cap de profit, ele diminuiu o sellPercentage para poder sair mais rápido das posições em aberto
            if (AppSettings.TradeConfiguration.CurrentProfit >= AppSettings.TradeConfiguration.MaxProfit)
                AppSettings.TradeConfiguration.SellPercentage = (decimal)0.1;
            else
                AppSettings.TradeConfiguration.SellPercentage = (decimal)0.6;

            Console.WriteLine($"SELL perc: {AppSettings.TradeConfiguration.SellPercentage}, PROFIT perc: {AppSettings.TradeConfiguration.CurrentProfit}, USDT: {AppSettings.TradeConfiguration.CurrentUSDTProfit}");
            try
            {
                #region manage open positions
                for (int i = 0; i < positions.Count; i++)
                {
                    var market = await _clientSvc.GetTicker(positions[i].Symbol);
                    decimal currentPrice = market.AskPrice;

                    decimal currentValorization = ValorizationCalculation(positions[i].LastPrice, currentPrice);

                    bool stop = false;

                    positions[i].LastPrice = currentPrice;
                    Console.WriteLine($"\nMANAGE: ticker {positions[i].Symbol}-{positions[i].Type}; current val {currentValorization}; last val {positions[i].Valorization}\n");
                    if (currentValorization <= 0)
                    {
                        var responseSell = await ValidationSellOrder(positions[i], currentValorization, market);
                        if (responseSell != null)
                        {
                            sold.Add(responseSell.Symbol);

                            int index = toMonitor.FindIndex(x => x.Symbol == responseSell.Symbol);
                            if (index > -1)
                                toMonitor[index] = responseSell;
                            else
                                toMonitor.Add(responseSell);

                            WalletManagement.RemovePositionFromFile(responseSell.Symbol, AppSettings.TradeConfiguration.CurrentProfit, AppSettings.TradeConfiguration.CurrentUSDTProfit);
                        }
                    }
                    else
                    {
                        while (!stop)
                        {
                            await Task.Delay(2000);
                            market = await _clientSvc.GetTicker(positions[i].Symbol);
                            currentPrice = market.AskPrice;

                            currentValorization = ValorizationCalculation(positions[i].LastPrice, currentPrice);
                            positions[i].Valorization = ValorizationCalculation(positions[i].InitialPrice, currentPrice);
                            Console.WriteLine("valorizacao somada: " + positions[i].Valorization);

                            if (positions[i].Valorization >= AppSettings.TradeConfiguration.SellPercentage)
                            {
                                Console.WriteLine("Current valorization");
                                stop = true;
                                var responseSell = await ValidationSellOrder(positions[i], currentValorization, market);

                                if (responseSell != null)
                                {
                                    // mandar para uma lista de monitoramento dessa moeda e marcar o preço que saiu pois só compra se subir X acima dele
                                    //positions[i] = responseSell;
                                    sold.Add(responseSell.Symbol);
                                    int index = toMonitor.FindIndex(x => x.Symbol == responseSell.Symbol);
                                    if (index > -1)
                                        toMonitor[index] = responseSell;
                                    else
                                        toMonitor.Add(responseSell);

                                    WalletManagement.RemovePositionFromFile(responseSell.Symbol, AppSettings.TradeConfiguration.CurrentProfit, AppSettings.TradeConfiguration.CurrentUSDTProfit);
                                }
                            }
                            else
                            {
                                positions[i].LastPrice = currentPrice;
                                stopCounter++;
                                if (stopCounter >= 4)
                                    stop = true;
                            }
                        }
                    }

                    positions[i].Valorization = ValorizationCalculation(positions[i].InitialPrice, currentPrice);
                }

                foreach (var obj in sold)
                    positions.RemoveAll(x => x.Symbol == obj);
                #endregion

                #region enter new positions

                if (AppSettings.TradeConfiguration.CurrentProfit < AppSettings.TradeConfiguration.MaxProfit && positions.Count < maxOpenPositions)
                {
                    for (int i = 0; i < opp.Minutes.Count && openMinutePositions < AppSettings.EngineConfiguration.MaxMinutePositions && positions.Count < maxOpenPositions; i++)
                    {
                        if (!alreadyUsed.Contains(opp.Minutes[i].Symbol))
                        {
                            var res = await ExecuteSimpleOrder(opp.Minutes[i].Symbol, RecommendationType.Minute);
                            if (res != null)
                            {
                                alreadyUsed.Add(opp.Minutes[i].Symbol);

                                openMinutePositions++;

                                res.Risk = -1;
                                positions.Add(res);

                                WalletManagement.AddPositionToFile(res, AppSettings.TradeConfiguration.CurrentProfit, AppSettings.TradeConfiguration.CurrentUSDTProfit);
                            }
                        }
                    }

                    for (int i = 0; i < opp.Days.Count && openDayPositions < AppSettings.EngineConfiguration.MaxDayPositions && positions.Count < maxOpenPositions; i++)
                    {
                        if (!alreadyUsed.Contains(opp.Days[i].Symbol))
                        {
                            var res = await ExecuteSimpleOrder(opp.Days[i].Symbol, RecommendationType.Day);
                            if (res != null)
                            {
                                alreadyUsed.Add(opp.Days[i].Symbol);

                                openDayPositions++;

                                res.Risk = -3;
                                positions.Add(res);

                                WalletManagement.AddPositionToFile(res, AppSettings.TradeConfiguration.CurrentProfit, AppSettings.TradeConfiguration.CurrentUSDTProfit);
                            }
                        }
                    }

                    for (int i = 0; i < opp.Hours.Count && openHourPositions < AppSettings.EngineConfiguration.MaxHourPositions && positions.Count < maxOpenPositions; i++)
                    {
                        if (!alreadyUsed.Contains(opp.Hours[i].Symbol))
                        {
                            var res = await ExecuteSimpleOrder(opp.Hours[i].Symbol, RecommendationType.Hour);
                            if (res != null)
                            {
                                alreadyUsed.Add(opp.Hours[i].Symbol);

                                openHourPositions++;

                                res.Risk = -2;
                                positions.Add(res);

                                WalletManagement.AddPositionToFile(res, AppSettings.TradeConfiguration.CurrentProfit, AppSettings.TradeConfiguration.CurrentUSDTProfit);
                            }
                        }
                    }
                }
                #endregion
                return new ManagerResponse(opp, positions, toMonitor);
            }
            catch (Exception ex)
            {
                _logger.LogError($"ERROR: {DateTime.Now}, metodo: PortfolioService.ManagePosition(), message: {ex.Message}, \n stack: {ex.StackTrace}");
                return new ManagerResponse(opp, positions, toMonitor);
            }
        }

        /// <summary>
        /// Calculate
        /// </summary>
        private void MaxPositionsPerType()
        {
            int maxOpenRunner = maxOpenPositions;
            while (maxOpenRunner > 0)
            {
                if (AppSettings.EngineConfiguration.Minute)
                {
                    AppSettings.EngineConfiguration.MaxMinutePositions += 1;
                    maxOpenRunner--;
                }
                if (AppSettings.EngineConfiguration.Hour && maxOpenRunner > 0)
                {
                    AppSettings.EngineConfiguration.MaxHourPositions += 1;
                    maxOpenRunner--;
                }
                if (AppSettings.EngineConfiguration.Day && maxOpenRunner > 0)
                {
                    AppSettings.EngineConfiguration.MaxDayPositions += 1;
                    maxOpenRunner--;
                }
            }
        }

        private void UpdateOpenPositionsPerType(List<Position> positions)
        {
            openDayPositions = 0;
            openHourPositions = 0;
            openMinutePositions = 0;

            for (int i = 0; i < positions.Count; i++)
            {
                var current = positions[i];

                switch (current.Type)
                {
                    case RecommendationType.Day:
                        openDayPositions++;
                        break;
                    case RecommendationType.Hour:
                        openHourPositions++;
                        break;
                    case RecommendationType.Minute:
                        openMinutePositions++;
                        break;
                }
            }
        }

        /// <summary>
        /// Executa a venda a partir de um motor de decisão.
        /// </summary>
        /// <param name="symbol"></param>
        /// <param name="quantity"></param>
        /// <returns></returns>
        public async Task<BinancePlacedOrder> ExecuteSellOrder(string symbol, decimal quantity)
        {
            Console.WriteLine("#### Entrou VENDA");
            decimal prevPrice = 0;
            int j = 0;

            while (j < 4)
            {
                await Task.Delay(2000);
                var market = await _clientSvc.GetTicker(symbol);
                decimal price = market.AskPrice;
                Console.WriteLine($"Preco {symbol}: {price}");

                if (j > 0 && price > prevPrice)
                {
                    Console.WriteLine("\n Caiu venda SUBINDO\n");
                   
                    var order = await _marketSvc.PlaceSellOrder(symbol, quantity);

                    if (order == null)
                        _logger.LogError($"#### #### #### #### #### #### ####\n\t### VENDA de {symbol} NAO EXECUTADA ###\n\t#### #### #### #### #### #### ####");
                    else
                        return order;
                }

                prevPrice = price;
                j++;
            }

            Console.WriteLine("\n Caiu venda FINAL\n");
            
            var final = await _marketSvc.PlaceSellOrder(symbol, quantity);

            if (final == null)
                _logger.LogWarning($"#### #### #### #### #### #### ####\n\t### VENDA de {symbol} NAO EXECUTADA ###\n\t#### #### #### #### #### #### ####");
            else
                return final;

            return null;
        }

        /// <summary>
        /// Retorna as posições em aberto, caso existam, da última execução interrompida do robô.
        /// </summary>
        /// <returns></returns>
        public List<Position> GetLastPositions()
        {
            try
            {
                List<Position> positions = WalletManagement.GetPositionFromFile();

                return positions;
            }
            catch (Exception ex)
            {
                _logger.LogError($"ERROR: {DateTime.Now}, metodo: PortfolioService.GetLastPositions(), message: {ex.Message}, \n stack: {ex.StackTrace}");
                return null;
            }

        }


        /// <summary>
        /// Faz a validação se vale ou não a pena vender o ativo, caso sim, determina o momento a partir de certas validações e executa.
        /// </summary>
        /// <param name="position">posição que será validada</param>
        /// <param name="currentValorization">variação atual</param>
        /// <param name="market">dados atuais de mercado da posição</param>
        /// <returns></returns>
        public async Task<Position> ValidationSellOrder(Position position, decimal currentValorization, IBinanceTick market)
        {
            Console.WriteLine("\nCaiu venda");
            if (position.Valorization >= AppSettings.TradeConfiguration.SellPercentage)
            {
                Console.WriteLine("\nCaiu validacao venda acima de 1");

                // esse if mantinha a posição se a valorização atual fosse maior que 0.3 porque poderia ser uma tendencia de subida
                //if (currentValorization <= (decimal)0.3)
                //{
                //    // executeSellOrder
                //    var order = await ExecuteSellOrder(position.Symbol, position.Quantity);
                //    if (order != null)
                //    {
                //        // jogar para um  novo objeto que sera usado para monitorar essa posicao caso volte a subir
                //        position.LastPrice = order.Price;
                //        position.LastValue = position.Quantity * order.Price;
                //        position.Valorization = ((order.Price - position.InitialPrice) / position.InitialPrice) * 100;
                //        _logger.LogWarning($"VENDA: {DateTime.Now}, moeda: {position.Symbol}, total valorization: {position.Valorization}, current price: {order.Price}, initial: {position.InitialPrice}");

                //        ReportLog.WriteReport(logType.VENDA, position);
                //        //position = new Position(market, order.Price, order.Quantity);

                //        return position;
                //    }
                //    return null;
                //}

                var order = await ExecuteSellOrder(position.Symbol, position.Quantity);
                if (order != null)
                {
                    // jogar para um  novo objeto que sera usado para monitorar essa posicao caso volte a subir
                    position.LastPrice = order.Price;
                    position.LastValue = position.Quantity * order.Price;
                    position.Valorization = ValorizationCalculation(position.InitialPrice, order.Price);
                    _logger.LogWarning($"VENDA: {DateTime.Now}, moeda: {position.Symbol}, total valorization: {position.Valorization}, current price: {order.Price}, initial: {position.InitialPrice}");

                    ReportLog.WriteReport(logType.VENDA, position);
                    //position = new Position(market, order.Price, order.Quantity);
                    AppSettings.TradeConfiguration.CurrentProfit += position.Valorization;
                    AppSettings.TradeConfiguration.CurrentUSDTProfit += (position.LastValue - position.InitialValue);

                    return position;
                }
                return null;
            }
            else if (currentValorization + position.Valorization <= position.Risk)
            {
                Console.WriteLine("\nCaiu venda RISCO");
                // executeSellOrder
                var order = await ExecuteSellOrder(position.Symbol, position.Quantity);
                if (order != null)
                {
                    // jogar para um  novo objeto que sera usado para monitorar essa posicao caso volte a subir
                    position.LastPrice = order.Price;
                    position.LastValue = position.Quantity * order.Price;
                    position.Valorization = ValorizationCalculation(position.InitialPrice, order.Price);
                    _logger.LogWarning($"VENDA: {DateTime.Now}, moeda: {position.Symbol}, total valorization: {position.Valorization}, current price: {order.Price}, initial: {position.InitialPrice}");

                    ReportLog.WriteReport(logType.VENDA, position);
                    //position = new Position(market, order.Price, order.Quantity);
                    AppSettings.TradeConfiguration.CurrentProfit += position.Valorization;
                    AppSettings.TradeConfiguration.CurrentUSDTProfit += (position.LastValue - position.InitialValue);
                    return position;
                }
                return null;
            }

            return null;
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
        /// <param name="symbol">símbolo que tentará ser comprado</param>
        /// <param name="type">utilizado para o log</param>
        /// <returns></returns>
        public async Task<Position> ExecuteSimpleOrder(string symbol, RecommendationType type)
        {
            decimal quantity = await GetUSDTAmount();
            if (quantity == 0)
                return null;

            Position position = new Position();

            decimal prevPrice = 0;
            int j = 0;

            while (j < 6)
            {
                await Task.Delay(3000);
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
                        _logger.LogInformation($"COMPRA: {DateTime.Now}, moeda: {symbol}, current percentage: {market.PriceChangePercent}, price: {order.Price}, type: {type}");
                        position = new Position(market, order.Price, order.Quantity);
                        position.Type = type;

                        ReportLog.WriteReport(logType.COMPRA, position);
                        j = 10;
                    }
                }

                prevPrice = price;
                j++;
            }

            return position.Data != null ? position : null;
        }

        public async Task<Position> ExecuteSimpleOrder(string symbol, RecommendationType type, decimal minPrice)
        {
            decimal quantity = await GetUSDTAmount();
            if (quantity == 0)
                return null;

            Position position = new Position();

            decimal prevPrice = 0;
            int j = 0;

            while (j < 6)
            {
                await Task.Delay(3000);
                var market = await _clientSvc.GetTicker(symbol);
                decimal price = market.AskPrice;

                if (j > 0 && price > prevPrice && price > minPrice)
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
                        position.Type = type;

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
                _logger.LogError($"ERROR: {DateTime.Now}, metodo: PortfolioService.GetBalance(), message: {ex.Message}, \n stack: {ex.StackTrace}");
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
                _logger.LogError($"ERROR: {DateTime.Now}, metodo: PortfolioService.GetBalance(), message: {ex.Message}, \n stack: {ex.StackTrace}");
                return null;
            }

        }

        private decimal ValorizationCalculation(decimal basePrice, decimal currentPrice)
        {
            return ((currentPrice - basePrice) / basePrice) * 100;
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
                _logger.LogError($"ERROR: {DateTime.Now}, metodo: PortfolioService.ManageOpenPositions(), message: {ex.Message}, \n stack: {ex.StackTrace}");
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
