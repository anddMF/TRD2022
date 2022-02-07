using Binance.Net.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;
using Trade02.Models.Trade;

namespace Trade02.Models.CrossCutting
{
    public class OrderResponse
    {
        public List<Position> Positions { get; set; }
        public List<IBinanceTick> Previous { get; set; }
        public List<string> OwnedSymbols { get; set; }

        public OrderResponse()
        { }

        public OrderResponse(List<Position> positions, List<string> ownedSymbols, List<IBinanceTick> previous)
        {
            Positions = positions;
            OwnedSymbols = ownedSymbols;
            Previous = previous;
        }
    }
}
