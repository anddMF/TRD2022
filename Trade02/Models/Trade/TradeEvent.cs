using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using Trade02.Infra.DAO;

namespace Trade02.Models.Trade
{
    public enum TradeEventType
    {
        BUY = 1, SELL, INFO, ERROR, START, FINISH
    }

    public class TradeEvent
    {
        public TradeEventType EventType { get; set; }
        public Position PositionData { get; set; }
        public DateTime Timestamp { get; set; }
        public string Payload { get; set; }

        public TradeEvent(TradeEventType eventType, DateTime timestamp, string payload, Position position = null)
        {
            if((eventType == TradeEventType.SELL || eventType == TradeEventType.BUY) && position == null)
                throw new ArgumentException("On a TradeEventType Sell or Buy, position must be a not null object");

            PositionData = position;
            Timestamp = timestamp;
            EventType = eventType;
            // Payload = Payload;

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

        public TradeEventDAO GenerateRecordDAO()
        {
            TradeEventDAO record = TransformToDAO();
            return record;
        }

        public string GenerateRecordJson()
        {
            string record = JsonConvert.SerializeObject(TransformToDAO());
            return record;
        }

        private TradeEventDAO TransformToDAO()
        {
            var dao = new TradeEventDAO();

            dao.client_id = 1;
            dao.event_type = (EventType)EventType;

            dao.asset = PositionData != null ? PositionData.Symbol : "";
            dao.rec_type = PositionData != null ? (RecommendationType)PositionData.Type : RecommendationType.MINUTE;
            dao.initial_price = PositionData != null ? PositionData.InitialPrice : decimal.Zero;
            dao.final_price = PositionData != null ? PositionData.LastPrice : decimal.Zero;
            dao.quantity = PositionData != null ? PositionData.Quantity : decimal.Zero;
            dao.valorization = PositionData != null ? PositionData.Valorization : decimal.Zero;

            dao.timestamp = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
            dao.message = Payload;

            return dao;
        }
    }
}
