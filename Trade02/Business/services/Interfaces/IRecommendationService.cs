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
        public Task<OpportunitiesResponse> CheckOppotunitiesByKlines(List<IBinanceTick> currentMarket, bool days, bool hours, bool minutes);
        public Task<bool> IsAKlineOpportunitie(string symbol, KlineInterval interval, int period);
        public OpportunitiesResponse RepurchaseValidation(OpportunitiesResponse opp, List<Position> assetList);
    }
}
