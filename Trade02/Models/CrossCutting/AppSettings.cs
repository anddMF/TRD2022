using System;
using System.Collections.Generic;
using System.Text;

namespace Trade02.Models.CrossCutting
{
    public class AppSettings
    {
        public static ApiConfiguration ApiConfiguration { get; set; }
        public static TradeConfiguration TradeConfiguration { get; set; }

        public static ApiConfiguration GetAPiConfiguration()
        {
            return ApiConfiguration;
        }
        public static TradeConfiguration GetTradeConfiguration()
        {
            return TradeConfiguration;
        }
    }

    public class ApiConfiguration
    {
        public string Key { get; set; }
        public string Secret { get; set; }
        public string Address { get; set; }
    }

    public class TradeConfiguration
    {
        public string Currency { get; set; }
        public decimal MaxBuyAmount { get; set; }
        public int MaxToMonitor { get; set; }
        public decimal MaxSearchPercentage { get; set; }
        public int MaxOpenPositions { get; set; }
        public List<string> OwnedSymbols { get; set; }
    }
}
