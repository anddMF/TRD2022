using Binance.Net.Interfaces;
using Binance.Net.Objects.Spot.MarketData;
using Binance.Net.Objects.Spot.SpotData;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
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
        private readonly decimal originalSellPercentage = AppSettings.TradeConfiguration.SellPercentage;
        private readonly int minUSDT = 15;

        private int openDayPositions = 0;
        private int openHourPositions = 0;
        private int openMinutePositions = 0;

        private HashSet<string> alreadyUsed;
        private List<string> sold;
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
        /// Engine for the managing of open positions and recommended ones. Based on certain conditions, it makes the decision for a sell or hold call, also, initiates the process 
        /// for a buy call on another engine.
        /// </summary>
        /// <param name="opp">opportunities </param>
        /// <param name="positions">posições que já estão em aberto</param>
        /// <returns></returns>
        public async Task<ManagerResponse> ManagePosition(OpportunitiesResponse opp, List<Position> positions, List<Position> toMonitor)
        {
            Console.WriteLine($"Recommendations per type: M={opp.Minutes.Count}; H={opp.Hours.Count}; D={opp.Days.Count}");

            // File where the user can write which symbol to sell or to shut down the program
            List<string> toSellList = WalletManagement.GetSellPositionFromFile();
            if (toSellList != null && toSellList.Count > 0)
            {
                if (toSellList[0].ToLower() == "shut")
                {
                    await ShutDownProgram(positions);
                }
                else
                {
                    foreach (string toSell in toSellList)
                    {
                        var position = positions.Find(x => x.Symbol.ToLower().StartsWith(toSell.ToLower()));
                        bool res = await ExecuteForceSell(position);

                        if (res)
                            positions.RemoveAll(x => x.Symbol == position.Symbol);
                    }
                }
            }

            alreadyUsed = new HashSet<string>(positions.ConvertAll(x => x.Symbol).ToList());
            UpdateOpenPositionsPerType(positions);

            sold = new List<string>();

            // if surpass the maximum cap of profits, it decreases the sellPercentage so it can get out of open positions more quickly
            if (AppSettings.TradeConfiguration.CurrentProfit >= AppSettings.TradeConfiguration.MaxProfit)
                AppSettings.TradeConfiguration.SellPercentage = (decimal)0.1;
            else
                AppSettings.TradeConfiguration.SellPercentage = originalSellPercentage;

            //TransmitTradeEvent(TradeEventType.INFO, $"SELL: {AppSettings.TradeConfiguration.SellPercentage}%, PROFIT: {AppSettings.TradeConfiguration.CurrentProfit}%, USDT: {AppSettings.TradeConfiguration.CurrentUSDTProfit}");
            Console.WriteLine(StringCurrentStatus());

            try
            {
                #region manage open positions

                foreach (var position in positions.ToList())
                {
                    var market = await _clientSvc.GetTicker(position.Symbol);
                    decimal currentPrice = market.AskPrice;

                    decimal currentValorization = ValorizationCalc(position.LastPrice, currentPrice);

                    Console.WriteLine($"\nMANAGE: ticker {position.Symbol}-{position.Type}; current val {Utils.FormatDecimal(currentValorization)}; last val {Utils.FormatDecimal(position.Valorization)}");

                    if (currentValorization <= 0)
                    {
                        var responseSell = await ValidationSellOrder(position, currentValorization, market);
                        if (responseSell != null)
                        {
                            toMonitor = UpdateToMonitorList(responseSell, toMonitor);
                        }
                    }
                    else
                    {
                        int stopCounter = 0;
                        while (stopCounter < 4)
                        {
                            await Task.Delay(2000);
                            market = await _clientSvc.GetTicker(position.Symbol);
                            currentPrice = market.AskPrice;

                            currentValorization = ValorizationCalc(position.LastPrice, currentPrice);
                            position.Valorization = ValorizationCalc(position.InitialPrice, currentPrice);
                            Console.WriteLine("current valorization: " + Utils.FormatDecimal(position.Valorization));

                            if (position.Valorization >= AppSettings.TradeConfiguration.SellPercentage)
                            {
                                Console.WriteLine("selling based on sellPercentage");
                                var responseSell = await ValidationSellOrder(position, currentValorization, market);
                                if (responseSell != null)
                                {
                                    toMonitor = UpdateToMonitorList(responseSell, toMonitor);
                                    break;
                                }
                            }
                            else
                            {
                                position.LastPrice = currentPrice;
                                stopCounter++;
                            }
                        }
                    }

                    position.Valorization = ValorizationCalc(position.InitialPrice, currentPrice);
                }

                foreach (var obj in sold)
                    positions.RemoveAll(x => x.Symbol == obj);

                #endregion

                #region enter new positions

                if (AppSettings.TradeConfiguration.CurrentProfit < AppSettings.TradeConfiguration.MaxProfit && positions.Count < maxOpenPositions)
                {
                    positions = await ExecuteOrder(positions, opp.Minutes, AppSettings.EngineConfiguration.MaxMinutePositions, maxOpenPositions, openMinutePositions, Position.RiskPerType(RecommendationTypeEnum.Minute), RecommendationTypeEnum.Minute);
                    positions = await ExecuteOrder(positions, opp.Hours, AppSettings.EngineConfiguration.MaxHourPositions, maxOpenPositions, openHourPositions, Position.RiskPerType(RecommendationTypeEnum.Hour), RecommendationTypeEnum.Hour);
                    positions = await ExecuteOrder(positions, opp.Days, AppSettings.EngineConfiguration.MaxDayPositions, maxOpenPositions, openDayPositions, Position.RiskPerType(RecommendationTypeEnum.Day), RecommendationTypeEnum.Day);
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

        private List<Position> UpdateToMonitorList(Position responseSell, List<Position> toMonitor)
        {
            sold.Add(responseSell.Symbol);
            int index = toMonitor.FindIndex(x => x.Symbol == responseSell.Symbol);
            if (index > -1)
                toMonitor[index] = responseSell;
            else
                toMonitor.Add(responseSell);

            return toMonitor;
        }

        #region methods that handle orders

        /// <summary>
        /// Execute an order only if the conditions are met: space available on open positions and the symbol isn`t already an open position
        /// </summary>
        /// <param name="positions"></param>
        /// <param name="symbols"></param>
        /// <param name="maxPositions"></param>
        /// <param name="maxOpenPositions"></param>
        /// <param name="openPositions"></param>
        /// <param name="riskLevel"></param>
        /// <param name="recommendationType"></param>
        /// <returns></returns>
        private async Task<List<Position>> ExecuteOrder(List<Position> positions, List<IBinanceTick> symbols, int maxPositions, int maxOpenPositions, int openPositions, decimal riskLevel, RecommendationTypeEnum recommendationType)
        {
            for (int i = 0; i < symbols.Count && openPositions < maxOpenPositions && positions.Count < maxPositions; i++)
            {
                if (!alreadyUsed.Contains(symbols[i].Symbol))
                {
                    var res = await ExecuteBuyOrder(symbols[i].Symbol, recommendationType);
                    if (res != null)
                    {
                        TransmitTradeEvent(TradeEventType.INFO, StringCurrentStatus());

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
                Console.WriteLine($"Price {symbol}: {Utils.FormatDecimal(price)}");

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
        /// Executes a force sell without attempting to get a better value
        /// </summary>
        /// <param name="position"></param>
        /// <returns></returns>
        private async Task<bool> ExecuteForceSell(Position position)
        {
            var order = await ExecuteSellOrder(position.Symbol, position.Quantity);
            if (order != null)
            {
                HandlePositionSold(position, order);
                return true;
            }
            return false;
        }

        private Position HandlePositionSold(Position position, BinancePlacedOrder order)
        {
            position.LastPrice = order.Price;
            position.LastValue = position.Quantity * order.Price;
            position.Valorization = ValorizationCalc(position.InitialPrice, order.Price);
            TransmitTradeEvent(TradeEventType.SELL, StringCurrentStatus(), position);

            ReportLog.WriteReport(logType.SELL, position);

            AppSettings.TradeConfiguration.CurrentProfit += position.Valorization;
            AppSettings.TradeConfiguration.CurrentUSDTProfit += position.LastValue - position.InitialValue;

            WalletManagement.RemovePositionFromFile(position.Symbol, AppSettings.TradeConfiguration.CurrentProfit, AppSettings.TradeConfiguration.CurrentUSDTProfit);

            return position;
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
                    position = HandlePositionSold(position, order);
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
                    position = HandlePositionSold(position, order);
                    return position;
                }
                return null;
            }

            return null;
        }

        /// <summary>
        /// Executes a single buy order that meets the constraints for it.
        /// </summary>
        /// <param name="symbol">symbol for the order</param>
        /// <param name="type"></param>
        /// <returns></returns>
        public async Task<Position> ExecuteBuyOrder(string symbol, RecommendationTypeEnum type)
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
                        _logger.LogInformation($"BUY: {DateTime.Now}, ASSET: {symbol}, CURRENT %: {Utils.FormatDecimal(market.PriceChangePercent)}, PRICE: {Utils.FormatDecimal(order.Price)}, TYPE: {type}");
                        position = new Position(market, order.Price, order.Quantity);
                        position.Type = type;

                        TransmitTradeEvent(TradeEventType.BUY, StringCurrentStatus(), position);
                        ReportLog.WriteReport(logType.BUY, position);
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
        public async Task<Position> ExecuteBuyOrder(string symbol, RecommendationTypeEnum type, decimal minPrice)
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

                        TransmitTradeEvent(TradeEventType.BUY, StringCurrentStatus(), position);
                        ReportLog.WriteReport(logType.BUY, position);
                        j = 10;
                    }
                }

                prevPrice = price;
                j++;
            }

            return position.Data != null ? position : null;
        }

        #endregion

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

            // max amout to be used
            totalUsdt = Math.Min(totalUsdt, maxBuyAmount);

            // formula to make orders with 15 usdt minimum
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

        private async Task ShutDownProgram(List<Position> positions)
        {
            _logger.LogInformation("###### STARTING FORCED SHUTDOWM #####");
            foreach (Position position in positions)
                await ExecuteForceSell(position);

            await Task.Delay(15000);
            Environment.Exit(0);
        }

        private decimal ValorizationCalc(decimal basePrice, decimal currentPrice)
        {
            return ((currentPrice - basePrice) / basePrice) * 100;
        }

        private string StringCurrentStatus()
        {
            return $"SELL %: {Utils.FormatDecimal(AppSettings.TradeConfiguration.SellPercentage)}%, PROFIT: {Utils.FormatDecimal(AppSettings.TradeConfiguration.CurrentProfit)}%, USDT: {Utils.FormatDecimal(AppSettings.TradeConfiguration.CurrentUSDTProfit)}";
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
    }
}
