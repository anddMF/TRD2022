﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Trade02.Models.CrossCutting
{
    public class AppSettings
    {
        public static ApiConfiguration ApiConfiguration { get; set; }
        public static TradeConfiguration TradeConfiguration { get; set; }
        public static EngineConfiguration EngineConfiguration { get; set; }

        public static ApiConfiguration GetAPiConfiguration()
        {
            return ApiConfiguration;
        }
        public static TradeConfiguration GetTradeConfiguration()
        {
            return TradeConfiguration;
        }
        public static EngineConfiguration GetEngineConfiguration()
        {
            return EngineConfiguration;
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
        public int MaxPositionMinutes { get; set; }
        public decimal MaxSearchPercentage { get; set; }
        public int MaxOpenPositions { get; set; }
        public int DaysToAnalyze { get; set; }
        public decimal CurrentUSDTProfit { get; set; }
        public decimal CurrentProfit { get; set; }
        public decimal MaxProfit { get; set; }
        public decimal SellPercentage { get; set; }
        public List<string> OwnedSymbols { get; set; }
    }
    
    public class EngineConfiguration
    {
        public bool Day { get; set; }
        public bool Hour { get; set; }
        public bool Minute { get; set; }
        public int MaxDayPositions { get; set; }
        public int MaxHourPositions { get; set; }
        public int MaxMinutePositions { get; set; }
    }
}
