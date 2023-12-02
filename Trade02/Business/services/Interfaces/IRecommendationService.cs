using Binance.Net.Enums;
using Binance.Net.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Trade02.Models.CrossCutting;
using Trade02.Models.Trade;

namespace Trade02.Business.services.Interfaces
{
    public interface IRecommendationService
    {
        /// <summary>
        /// Iterates on the list 'currentMarket' and try to identify opportunities using the timed klines from the asset.
        /// </summary>
        /// <param name="currentMarket">filtered list of assets</param>
        /// <param name="days">switch to check for opportunities of type 'days'</param>
        /// <param name="hours">switch to check for opportunities of type 'hours'</param>
        /// <param name="minutes">switch to check for opportunities of type 'minutes'</param>
        /// <returns></returns>
        public Task<OpportunitiesResponse> CheckOpportunitiesByKlines(List<IBinanceTick> currentMarket, bool days, bool hours, bool minutes);

        /// <summary>
        /// Verifies if the symbol has a favorable buy status based on it's last klines.
        /// </summary>
        /// <param name="symbol"></param>
        /// <param name="interval"></param>
        /// <param name="period"></param>
        /// <returns></returns>
        public Task<bool> IsAKlineOpportunity(string symbol, KlineInterval interval, int period);

        /// <summary>
        /// Responsible to verify on the list of recommendations if contains positions that were already sold before, if so, only maintains in the list the ones that have a price 1% higher compared to the sold price.
        /// </summary>
        /// <param name="opp">list of recommendations</param>
        /// <param name="assetList">list of already sold positions</param>
        /// <returns></returns>
        public OpportunitiesResponse RepurchaseValidation(OpportunitiesResponse opp, List<Position> assetList);
    }
}
