using Binance.Net.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;
using Trade02.Models.Trade;

namespace Trade02.Models.CrossCutting
{
    public class PortfolioResponse
    {
        public List<IBinanceTick> MonitorData { get; set; }
        public List<Position> OpenPositions { get; set; }

        public PortfolioResponse()
        {

        }

        public PortfolioResponse(List<IBinanceTick> monitor, List<Position> openPositions)
        {
            MonitorData = monitor;
            OpenPositions = openPositions;
        }
    }
}
