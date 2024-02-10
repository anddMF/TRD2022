using Binance.Net.Interfaces;
using Binance.Net.Objects.Spot.MarketData;
using Binance.Net.Objects.Spot.SpotData;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Trade02.Models.CrossCutting;
using Trade02.Models.Trade;

namespace Trade02.Business.services
{
    /// <summary>
    /// Responsible for the management of the assets.
    /// </summary>
    public interface IPortfolioService
    {
        /// <summary>
        /// Engine for the managing of open positions and recommended ones. Based on certain conditions, it makes the decision for a sell or hold call, also, initiates the process 
        /// for a buy call on another engine.
        /// </summary>
        /// <param name="opp">opportunities </param>
        /// <param name="positions">posições que já estão em aberto</param>
        /// <returns></returns>
        public Task<ManagerResponse> ManagePosition(OpportunitiesResponse opp, List<Position> positions, List<Position> toMonitor);

        /// <summary>
        /// Executes a sell call based on certain validations and attempts.
        /// </summary>
        /// <param name="symbol"></param>
        /// <param name="quantity"></param>
        /// <returns></returns>
        public Task<BinancePlacedOrder> ExecuteSellOrder(string symbol, decimal quantity);

        /// <summary>
        /// Returns the positions that remained open from the last execution of the TRD2022.
        /// </summary>
        /// <returns></returns>
        public List<Position> GetLastPositions();

        /// <summary>
        /// Balances if it is a good moment to sell an asset, if yes, determines a moment to sell it based on certain validations and make the call.
        /// </summary>
        /// <param name="position">open position</param>
        /// <param name="currentValorization">current valorization</param>
        /// <param name="market">current market data from the position</param>
        /// <returns></returns>
        public Task<Position> ValidationSellOrder(Position position, decimal currentValorization, IBinanceTick market);

        /// <summary>
        /// Executes a single buy order that meets the constraints for it.
        /// </summary>
        /// <param name="symbol">symbol for the order</param>
        /// <param name="type"></param>
        /// <returns></returns>
        public Task<Position> ExecuteBuyOrder(string symbol, RecommendationTypeEnum type);

        /// <summary>
        /// Executes a single buy order that meets the constraints for it and the minimum price.
        /// </summary>
        /// <param name="symbol">symbol for the order</param>
        /// <param name="type"></param>
        /// <param name="minPrice"></param>
        /// <returns></returns>
        public Task<Position> ExecuteBuyOrder(string symbol, RecommendationTypeEnum type, decimal minPrice);

        /// <summary>
        /// Get the amount of USDT that can be spent on an order.
        /// </summary>
        /// <returns></returns>
        public Task<decimal> GetUSDTAmount();

        /// <summary>
        /// Gets the balance from the assets on the wallet.
        /// </summary>
        /// <returns></returns>
        public Task<List<BinanceBalance>> GetBalance();

        /// <summary>
        /// Gets the balance from one specific asset on the wallet.
        /// </summary>
        /// <param name="symbol"></param>
        /// <returns></returns>
        public Task<BinanceBalance> GetBalance(string symbol);
        public Task<BinanceOrderBook> GetOrderBook(string symbol, int limit);

    }
}
