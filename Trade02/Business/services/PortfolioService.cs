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
using Trade02.Business.services.Interfaces;
using Trade02.Infra.Cross;
using Trade02.Infra.DAL.Interfaces;
using Trade02.Models.CrossCutting;
using Trade02.Models.Trade;
using static Trade02.Infra.Cross.ReportLog;

namespace Trade02.Business.services
{
    /// <summary>
    /// Responsible for the management of the assets.
    /// </summary>
    public class PortfolioService : IPortfolioService
    {
        #region setup variables
        private static IAPICommunication _clientSvc;
        private static IMarketService _marketSvc;
        private static IEventsOutput _eventsOutput;
        private readonly ILogger _logger;

        private readonly int maxOpenPositions = AppSettings.TradeConfiguration.MaxOpenPositions;
        private readonly decimal maxBuyAmount = AppSettings.TradeConfiguration.MaxBuyAmount;
        private readonly bool freeMode = AppSettings.TradeConfiguration.FreeMode;
        private readonly int minUSDT = 15;

        private int openDayPositions = 0;
        private int openHourPositions = 0;
        private int openMinutePositions = 0;
        #endregion

        public PortfolioService(ILogger<PortfolioService> logger, IEventsOutput eventsOutput, IMarketService marketSvc, IAPICommunication clientSvc)
        {
            _logger = logger;
            _eventsOutput = eventsOutput;
            _clientSvc = clientSvc;
            _marketSvc = marketSvc;

            MaxPositionsPerType();
        }

        /// <summary>
        /// Responsible for the transmission of the trade event for the EventsOutput
        /// </summary>
        /// <param name="type"></param>
        /// <param name="message"></param>
        /// <param name="position"></param>
        private async void TransmitTradeEvent(TradeEventType type, string message, Position position = null)
        {
            bool sent = await _eventsOutput.SendEvent(new TradeEvent(type, DateTime.Now, message, position));
            // TODO: treatment for an event not sent
        }

        private async Task<bool> ExecuteForceSell(Position position)
        {
            var order = await ExecuteSellOrder(position.Symbol, position.Quantity);
            if (order != null)
            {
                position.LastPrice = order.Price;
                position.LastValue = position.Quantity * order.Price;
                position.Valorization = ValorizationCalc(position.InitialPrice, order.Price);
                TransmitTradeEvent(TradeEventType.SELL, "", position);

                ReportLog.WriteReport(logType.VENDA, position);
                
                AppSettings.TradeConfiguration.CurrentProfit += position.Valorization;
                AppSettings.TradeConfiguration.CurrentUSDTProfit += (position.LastValue - position.InitialValue);

                WalletManagement.RemovePositionFromFile(position.Symbol, AppSettings.TradeConfiguration.CurrentProfit, AppSettings.TradeConfiguration.CurrentUSDTProfit);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Engine for the managing of open positions and recommended ones. Based on certain conditions, it makes the decision for a sell or hold call, also, initiates the process 
        /// for a buy call on another engine.
        /// </summary>
        /// <param name="opp">opportunities </param>
        /// <param name="positions">posições que já estão em aberto</param>
        /// <returns></returns>
        public async Task<ManagerResponse> ManagePosition(OpportunitiesResponse opp, List<Position> positions, List<Position> toMonitor)
        {
            Console.WriteLine("Recommendation count: " + opp.Minutes.Count);

            // File where the user can write which symbol to sell or to shut down the program
            List<string> toSellList = WalletManagement.GetSellPositionFromFile();
            if (toSellList.Count > 0)
            {
                if (toSellList[0].ToLower() == "shut down")
                {
                    foreach(Position position in positions)
                        await ExecuteForceSell(position);
                    
                    Environment.Exit(0);
                } else
                {
                    foreach(string toSell in toSellList)
                    {
                        var position = positions.Find(x => x.Symbol.ToLower().StartsWith(toSell.ToLower()));
                        bool res = await ExecuteForceSell(position);

                        if (res)
                            positions.RemoveAll(x => x.Symbol == position.Symbol);
                    }
                }
            }

            HashSet<string> alreadyUsed = new HashSet<string>(positions.ConvertAll(x => x.Symbol).ToList());
            UpdateOpenPositionsPerType(positions);

            int stopCounter = 0;
            var sold = new List<string>();

            // if surpass the maximum cap of profits, it decreases the sellPercentage so it can get out of open positions more quickly
            if (AppSettings.TradeConfiguration.CurrentProfit >= AppSettings.TradeConfiguration.MaxProfit)
                AppSettings.TradeConfiguration.SellPercentage = (decimal)0.1;
            else
                AppSettings.TradeConfiguration.SellPercentage = (decimal)0.4;

            //TransmitTradeEvent(TradeEventType.INFO, $"SELL: {AppSettings.TradeConfiguration.SellPercentage}%, PROFIT: {AppSettings.TradeConfiguration.CurrentProfit}%, USDT: {AppSettings.TradeConfiguration.CurrentUSDTProfit}");
            Console.WriteLine($"SELL: {AppSettings.TradeConfiguration.SellPercentage}%, PROFIT: {AppSettings.TradeConfiguration.CurrentProfit}%, USDT: {AppSettings.TradeConfiguration.CurrentUSDTProfit}");

            try
            {
                #region manage open positions
                for (int i = 0; i < positions.Count; i++)
                {
                    var market = await _clientSvc.GetTicker(positions[i].Symbol);
                    decimal currentPrice = market.AskPrice;

                    decimal currentValorization = ValorizationCalc(positions[i].LastPrice, currentPrice);

                    bool stop = false;

                    positions[i].LastPrice = currentPrice;
                    Console.WriteLine($"\nMANAGE: ticker {positions[i].Symbol}-{positions[i].Type}; current val {currentValorization}; last val {positions[i].Valorization}\n");
                    if (currentValorization <= 0)
                    {
                        var responseSell = await ValidationSellOrder(positions[i], currentValorization, market);
                        if (responseSell != null)
                        {
                            TransmitTradeEvent(TradeEventType.INFO, $"SELL: {AppSettings.TradeConfiguration.SellPercentage}%, PROFIT: {AppSettings.TradeConfiguration.CurrentProfit}%, USDT: {AppSettings.TradeConfiguration.CurrentUSDTProfit}");
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

                            currentValorization = ValorizationCalc(positions[i].LastPrice, currentPrice);
                            positions[i].Valorization = ValorizationCalc(positions[i].InitialPrice, currentPrice);
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
                                    TransmitTradeEvent(TradeEventType.INFO, $"SELL: {AppSettings.TradeConfiguration.SellPercentage}%, PROFIT: {AppSettings.TradeConfiguration.CurrentProfit}%, USDT: {AppSettings.TradeConfiguration.CurrentUSDTProfit}");

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

                    positions[i].Valorization = ValorizationCalc(positions[i].InitialPrice, currentPrice);
                }

                foreach (var obj in sold)
                    positions.RemoveAll(x => x.Symbol == obj);
                #endregion

                #region enter new positions

                if (AppSettings.TradeConfiguration.CurrentProfit < AppSettings.TradeConfiguration.MaxProfit && positions.Count < maxOpenPositions)
                {
                    positions = await ExecuteOrder(positions, opp.Minutes, AppSettings.EngineConfiguration.MaxMinutePositions, maxOpenPositions, openMinutePositions, (decimal)-0.3, RecommendationTypeEnum.Minute, alreadyUsed);
                    positions = await ExecuteOrder(positions, opp.Hours, AppSettings.EngineConfiguration.MaxHourPositions, maxOpenPositions, openHourPositions, -2, RecommendationTypeEnum.Hour, alreadyUsed);
                    positions = await ExecuteOrder(positions, opp.Days, AppSettings.EngineConfiguration.MaxDayPositions, maxOpenPositions, openDayPositions, -3, RecommendationTypeEnum.Day, alreadyUsed);
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

        private async Task<List<Position>> ExecuteOrder(List<Position> positions, List<IBinanceTick> symbols, int maxPositions, int maxOpenPositions, int openPositions, decimal riskLevel, RecommendationTypeEnum recommendationType, HashSet<string> alreadyUsed)
        {
            for (int i = 0; i < symbols.Count && openPositions < maxOpenPositions && positions.Count < maxPositions; i++)
            {
                if (!alreadyUsed.Contains(symbols[i].Symbol))
                {
                    var res = await ExecuteSimpleOrder(symbols[i].Symbol, recommendationType);
                    if (res != null)
                    {
                        TransmitTradeEvent(TradeEventType.INFO, $"SELL: {AppSettings.TradeConfiguration.SellPercentage}%, PROFIT: {AppSettings.TradeConfiguration.CurrentProfit}%, USDT: {AppSettings.TradeConfiguration.CurrentUSDTProfit}");

                        alreadyUsed.Add(symbols[i].Symbol);
                        openPositions++;
                        res.Risk = riskLevel;
                        positions.Add(res);

                        WalletManagement.AddPositionToFile(res, AppSettings.TradeConfiguration.CurrentProfit, AppSettings.TradeConfiguration.CurrentUSDTProfit);
                    }
                }
            }

            return positions;
        }

        /// <summary>
        /// Calculate how many spots are available for each type of recommendation.
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

        /// <summary>
        /// Updates the spots left for new positions per type of recommendation. Based on the current open positions, determines how many spots are left for each type of recommendation and stores
        /// it on global variables.
        /// </summary>
        /// <param name="positions">current open positions</param>
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
                    case RecommendationTypeEnum.Day:
                        openDayPositions++;
                        break;
                    case RecommendationTypeEnum.Hour:
                        openHourPositions++;
                        break;
                    case RecommendationTypeEnum.Minute:
                        openMinutePositions++;
                        break;
                }
            }
        }

        /// <summary>
        /// Executes a sell call based on certain validations and attempts.
        /// </summary>
        /// <param name="symbol"></param>
        /// <param name="quantity"></param>
        /// <returns></returns>
        public async Task<BinancePlacedOrder> ExecuteSellOrder(string symbol, decimal quantity)
        {
            Console.WriteLine("#### entered SELL");
            decimal prevPrice = 0;
            int j = 0;

            while (j < 4)
            {
                await Task.Delay(2000);
                var market = await _clientSvc.GetTicker(symbol);
                decimal price = market.AskPrice;
                Console.WriteLine($"Price {symbol}: {price}");

                if (j > 0 && price > prevPrice)
                {
                    Console.WriteLine("\n entered sell but going up\n");

                    var order = await _marketSvc.PlaceSellOrder(symbol, quantity);

                    if (order == null)
                        TransmitTradeEvent(TradeEventType.ERROR, $"SELL OF {symbol} NOT EXECUTED");
                    else
                        return order;
                }

                prevPrice = price;
                j++;
            }

            Console.WriteLine("\n entered last chance of sell\n");

            var final = await _marketSvc.PlaceSellOrder(symbol, quantity);

            if (final == null)
                TransmitTradeEvent(TradeEventType.ERROR, $"SELL OF {symbol} NOT EXECUTED");
            else
                return final;

            return null;
        }

        /// <summary>
        /// Returns the positions that remained open from the last execution of the TRD2022.
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
                _logger.LogError($"ERROR: {DateTime.Now}, method: PortfolioService.GetLastPositions(), message: {ex.Message}, \n stack: {ex.StackTrace}");
                return null;
            }
        }


        /// <summary>
        /// Balances if it is a good moment to sell an asset, if yes, determines a moment to sell it based on certain validations and make the call.
        /// </summary>
        /// <param name="position">open position</param>
        /// <param name="currentValorization">current valorization</param>
        /// <param name="market">current market data from the position</param>
        /// <returns></returns>
        public async Task<Position> ValidationSellOrder(Position position, decimal currentValorization, IBinanceTick market)
        {
            Console.WriteLine("\n #### entered sell validation");
            if (position.Valorization >= AppSettings.TradeConfiguration.SellPercentage)
            {
                Console.WriteLine("\n entered sell above sellPercentage");

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
                    position.LastPrice = order.Price;
                    position.LastValue = position.Quantity * order.Price;
                    position.Valorization = ValorizationCalc(position.InitialPrice, order.Price);
                    //_logger.LogWarning($"VENDA: {DateTime.Now}, moeda: {position.Symbol}, total valorization: {position.Valorization}, current price: {order.Price}, initial: {position.InitialPrice}");
                    TransmitTradeEvent(TradeEventType.SELL, "", position);

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
                Console.WriteLine("\n entered sell validation with RISK condition");
                // executeSellOrder
                var order = await ExecuteSellOrder(position.Symbol, position.Quantity);
                if (order != null)
                {
                    // jogar para um  novo objeto que sera usado para monitorar essa posicao caso volte a subir
                    position.LastPrice = order.Price;
                    position.LastValue = position.Quantity * order.Price;
                    position.Valorization = ValorizationCalc(position.InitialPrice, order.Price);
                    // _logger.LogWarning($"VENDA: {DateTime.Now}, moeda: {position.Symbol}, total valorization: {position.Valorization}, current price: {order.Price}, initial: {position.InitialPrice}");
                    TransmitTradeEvent(TradeEventType.SELL, "", position);

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
        /// Executes multiple orders based on certain condidtions.
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
                            TransmitTradeEvent(TradeEventType.ERROR, $"PURCHASE OF {symbol} NOT EXECUTED");
                        }
                        else
                        {
                            //_logger.LogInformation($"COMPRA: {DateTime.Now}, moeda: {symbol}, current percentage: {market.PriceChangePercent}, price: {order.Price}");
                            bought.Add(symbols[i]);

                            Position position = new Position(market, order.Price, order.Quantity);
                            openPositions.Add(position);

                            TransmitTradeEvent(TradeEventType.BUY, "", position);
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
        /// Executes a single buy order that meets the constraints for it.
        /// </summary>
        /// <param name="symbol">symbol for the order</param>
        /// <param name="type"></param>
        /// <returns></returns>
        public async Task<Position> ExecuteSimpleOrder(string symbol, RecommendationTypeEnum type)
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
                        TransmitTradeEvent(TradeEventType.ERROR, $"PURCHASE OF {symbol} NOT EXECUTED");
                    }
                    else
                    {
                        _logger.LogInformation($"COMPRA: {DateTime.Now}, moeda: {symbol}, current percentage: {market.PriceChangePercent}, price: {order.Price}, type: {type}");
                        position = new Position(market, order.Price, order.Quantity);
                        position.Type = type;

                        TransmitTradeEvent(TradeEventType.BUY, "", position);
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
        /// Executes a single buy order that meets the constraints for it and the minimum price.
        /// </summary>
        /// <param name="symbol">symbol for the order</param>
        /// <param name="type"></param>
        /// <param name="minPrice"></param>
        /// <returns></returns>
        public async Task<Position> ExecuteSimpleOrder(string symbol, RecommendationTypeEnum type, decimal minPrice)
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
                        //_logger.LogWarning($"#### #### #### #### #### #### ####\n\t### PURCHASE OF {symbol} NOT EXECUTED ###\n\t#### #### #### #### #### #### ####");
                        TransmitTradeEvent(TradeEventType.ERROR, $"PURCHASE OF {symbol} NOT EXECUTED");
                    }
                    else
                    {
                        // _logger.LogInformation($"COMPRA: {DateTime.Now}, moeda: {symbol}, current percentage: {market.PriceChangePercent}, price: {order.Price}");
                        position = new Position(market, order.Price, order.Quantity);
                        position.Type = type;

                        TransmitTradeEvent(TradeEventType.BUY, "", position);
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
            if (freeMode)
                return maxBuyAmount;

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
                TransmitTradeEvent(TradeEventType.ERROR, "INSUFFICIENT USDT BALANCE FOR PURCHASES");
                return 0;
            }

            return Math.Max(quantity, supportQuantity);
        }

        /// <summary>
        /// Gets the balance from the assets on the wallet.
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
        /// Gets the balance from one specific asset on the wallet.
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

        private decimal ValorizationCalc(decimal basePrice, decimal currentPrice)
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
