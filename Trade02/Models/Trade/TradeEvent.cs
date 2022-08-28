using System;
using System.Collections.Generic;
using System.Text;
using Trade02.Infra.DAO;

namespace Trade02.Models.Trade
{
    public enum TradeEventType
    {
        BUY, SELL, INFO, ERROR
    }

    public class TradeEvent
    {
        public TradeEventType EventType { get; set; }
        public Position PositionData { get; set; }
        public DateTime Timestamp { get; set; }
        public string Payload { get; set; }

        public TradeEvent(TradeEventType eventType, DateTime timestamp, string payload, Position position = null)
        {
            if(position != null && (eventType != TradeEventType.SELL || eventType != TradeEventType.BUY))
                throw new ArgumentException("On a TradeEventType Sell or Buy, position must be a not null object");

            PositionData = position;
            Timestamp = timestamp;
            EventType = eventType;

            if (eventType == TradeEventType.BUY)
                Payload = PayloadFromBuy();
            else if (eventType == TradeEventType.SELL)
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

        public TradeEventDAO GenerateRecord()
        {
            var record = new TradeEventDAO();

            record.client_id = 0;
            record.asset = PositionData.Symbol;
            record.event_type = (EventType) EventType;
            record.rec_type = (RecommendationType)PositionData.Type;
            record.initial_price = PositionData.InitialPrice;
            record.final_price = PositionData.LastPrice;
            record.quantity = PositionData.Quantity;
            record.valorization = PositionData.Valorization;
            record.timestamp = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
            record.message = Payload;

            return record;
        }
    }
}
