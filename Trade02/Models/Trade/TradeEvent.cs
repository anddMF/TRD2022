using System;
using System.Collections.Generic;
using System.Text;

namespace Trade02.Models.Trade
{
    public class TradeEvent
    {
        public TradeEventType EventType { get; set; }
        public Position PositionData { get; set; }
        public DateTime Timestamp { get; set; }
        public string Payload { get; set; }


        public TradeEvent(TradeEventType eventType, DateTime timestamp, string payload, Position position = null)
        {
            if(position != null && (eventType != TradeEventType.Sell || eventType != TradeEventType.Buy))
                throw new ArgumentException("On a TradeEventType Sell or Buy, position must be a not null object");

            PositionData = position;
            Timestamp = timestamp;
            EventType = eventType;

            if (eventType == TradeEventType.Buy)
                Payload = PayloadFromBuy();
            else if (eventType == TradeEventType.Sell)
                Payload = PayloadFromSell();
            else
                Payload = payload;
        }
        
        private string PayloadFromBuy()
        {
            return $"COMPRA: {DateTime.Now}, moeda: {PositionData.Symbol}, price: {PositionData.InitialPrice}, type: {PositionData.Type}";
            //return $"COMPRA: {DateTime.Now}, moeda: {PositionData.Symbol}, current percentage: {market.PriceChangePercent}, price: {PositionData.InitialPrice}, type: {PositionData.Type}";
        }
        private string PayloadFromSell()
        {
            return $"VENDA: {DateTime.Now}, moeda: {PositionData.Symbol}, total valorization: {PositionData.Valorization}, final price: {PositionData.LastPrice}, initial price: {PositionData.InitialPrice}, type: {PositionData.Type}";
        }
    }

    public enum TradeEventType
    {
        Buy = 0,
        Sell = 1,
        Update = 2,
        Error = 3
    }
}
