using System;
using System.Collections.Generic;
using System.Text;
using Trade02.Models.Trade;

namespace Trade02.Models.CrossCutting
{
    public class ManagerResponse
    {
        public OpportunitiesResponse Opportunities { get; set; }
        public List<Position> OpenPositions { get; set; }
        public List<Position> ToMonitor { get; set; }

        public ManagerResponse()
        {

        }

        public ManagerResponse(OpportunitiesResponse opportunities, List<Position> openPositions, List<Position> toMonitor)
        {
            Opportunities = opportunities;
            OpenPositions = openPositions;
            ToMonitor = toMonitor;
        }
    }
}
