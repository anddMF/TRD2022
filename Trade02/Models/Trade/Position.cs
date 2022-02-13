using Binance.Net.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace Trade02.Models.Trade
{
    public class Position
    {
        public IBinanceTick Data { get; set; }
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
        public RecommendationType Type { get; set; }

        public Position()
        { }

        public Position(IBinanceTick data, decimal orderPrice, decimal quantity)
        {
            Data = data;
            Valorization = 0;
            InitialPrice = orderPrice;
            LastMaxPrice = orderPrice;
            InitialValue = orderPrice * quantity;
            Quantity = quantity;
            LastPrice = orderPrice;
            LastValue = InitialValue;
            Minutes = 1;
            Risk = -10;
            Type = RecommendationType.Day;
        }

    }

    public enum RecommendationType
    {
        Day = 0,
        Hour = 1,
        Minute = 2
    }
}
