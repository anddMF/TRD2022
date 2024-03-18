using Binance.Net.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace Trade02.Models.Trade
{
    public class Position
    {
        public IBinanceTick Data { get; set; }
        public string Symbol { get; set; }
        public decimal Valorization { get; set; }
        public decimal InitialValue { get; set; }
        public decimal InitialPrice { get; set; }
        public decimal LastMaxPrice { get; set; }
        /// <summary>
        /// O último valor total da criptomoeda (quantity * price)
        /// </summary>
        public decimal LastValue { get; set; }
        public decimal LastPrice { get; set; }
        public decimal Quantity { get; set; }
        public int Minutes { get; set; }
        public decimal Risk { get; set; }
        public RecommendationTypeEnum Type { get; set; }

        public Position()
        { }

        public Position(IBinanceTick data, decimal orderPrice, decimal quantity)
        {
            Data = data;
            Symbol = data.Symbol;
            Valorization = 0;
            InitialPrice = orderPrice;
            LastMaxPrice = orderPrice;
            InitialValue = orderPrice * quantity;
            Quantity = quantity;
            LastPrice = orderPrice;
            LastValue = InitialValue;
            Minutes = 1;
            Risk = -10;
            Type = RecommendationTypeEnum.Day;
        }

        public Position(string symbol, decimal orderPrice, decimal quantity)
        {
            Symbol = symbol.Trim();
            Valorization = 0;
            InitialPrice = orderPrice;
            LastMaxPrice = orderPrice;
            InitialValue = orderPrice * quantity;
            Quantity = quantity;
            LastPrice = orderPrice;
            LastValue = InitialValue;
            Minutes = 1;
            Risk = -10;
            Type = RecommendationTypeEnum.Day;
        }

        public Position(string symbol, decimal orderPrice, decimal quantity, RecommendationTypeEnum type)
        {
            Symbol = symbol.Trim();
            Valorization = 0;
            InitialPrice = orderPrice;
            LastMaxPrice = orderPrice;
            InitialValue = orderPrice * quantity;
            Quantity = quantity;
            LastPrice = orderPrice;
            LastValue = InitialValue;
            Minutes = 1;
            Risk = RiskPerType(type);
            Type = type;
        }

        public static decimal RiskPerType(RecommendationTypeEnum type)
        {
            switch (type)
            {
                case RecommendationTypeEnum.Day:
                    return -1;
                case RecommendationTypeEnum.Hour:
                    return (decimal)-0.2;
                case RecommendationTypeEnum.Minute:
                    return (decimal)-0.1;
                default:
                    return (decimal)-0.3;
            }
        }
    }

    public enum RecommendationTypeEnum
    {
        Day = 0,
        Hour = 1,
        Minute = 2
    }
}
