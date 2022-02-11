using Binance.Net.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace Trade02.Models.CrossCutting
{
    public class OpportunitiesResponse
    {
        public List<IBinanceTick> Days { get; set; }
        public List<IBinanceTick> Hours { get; set; }
        public List<IBinanceTick> Minutes { get; set; }

        public OpportunitiesResponse()
        {

        }

        public OpportunitiesResponse(List<IBinanceTick> days, List<IBinanceTick> hours, List<IBinanceTick> minutes)
        {
            Days = days;
            Hours = hours;
            Minutes = minutes;
        }

    }
}
