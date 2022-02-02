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
        public decimal InitialCost { get; set; }
        public decimal LastValue { get; set; }
        public decimal LastCost { get; set; }

        public Position()
        { }

        public Position(IBinanceTick data, decimal orderPrice, decimal quantity)
        {
            Data = data;
            Valorization = 0;
            InitialCost = orderPrice;
            InitialValue = orderPrice * quantity;
            LastValue = 0;
        }

    }
}
