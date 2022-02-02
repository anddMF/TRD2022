using System;
using System.Collections.Generic;
using System.Text;
using Trade02.Models.Trade;

namespace Trade02.Models.CrossCutting
{
    public class OrderEngine
    {
        public List<Position> Positions { get; set; }
        public List<string> OwnedSymbols { get; set; }

        public OrderEngine()
        { }

        public OrderEngine(List<Position> positions, List<string> ownedSymbols)
        {
            Positions = positions;
            OwnedSymbols = ownedSymbols;
        }
    }
}
