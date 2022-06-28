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
        public Task<bool> IsAKlineOpportunitie(string symbol, KlineInterval interval, int period);
        public OpportunitiesResponse RepurchaseValidation(OpportunitiesResponse opp, List<Position> assetList);
    }
}
