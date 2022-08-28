using System;
using System.Collections.Generic;
using System.Text;

namespace Trade02.Infra.DAO
{
    public class TradeEventDAO
    {
        public EventType event_type { get; set; }
        public RecommendationType rec_type { get; set; }
        public string asset { get; set; }
        public decimal initial_price { get; set; }
        public decimal final_price { get; set; }
        public decimal quantity { get; set; }
        public decimal valorization { get; set; }
        public string timestamp { get; set; }
        public int client_id { get; set; }
        public string message { get; set; }

        public TradeEventDAO()
        {

        }

    }

    public enum EventType
    {
        BUY, SELL, INFO, ERROR
    }

    public enum RecommendationType
    {
        DAY, HOUR, MINUTE
    }
}
