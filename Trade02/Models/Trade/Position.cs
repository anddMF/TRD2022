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
        public decimal CurrentPrice { get; set; }
        /// <summary>
        /// O último valor total da criptomoeda (quantity * price)
        /// </summary>
        public decimal LastValue { get; set; }
        public decimal LastPrice { get; set; }
        public decimal Quantity { get; set; }

        public Position()
        { }

        public Position(IBinanceTick data, decimal orderPrice, decimal quantity)
        {
            Data = data;
            Valorization = 0;
            InitialPrice = orderPrice;
            CurrentPrice = orderPrice;
            InitialValue = orderPrice * quantity;
            Quantity = quantity;
            LastPrice = 0;
            LastValue = InitialValue;
        }

    }
}
