using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Trade02.Infra.DAL.Interfaces;
using Trade02.Models.CrossCutting;
using Trade02.Models.Trade;

namespace Trade02.Infra.Cross
{
    public class EventsOutput : IEventsOutput
    {
        private readonly bool isKafkaEnabled = AppSettings.KafkaConfiguration.Enabled;

        // send output
        private readonly ILogger _logger;
        private static IEventExtCommunication _kafkaSvc;
        public EventsOutput(ILogger<EventsOutput> logger, IEventExtCommunication kafkaSvc)
        {
            _logger = logger;
            _kafkaSvc = kafkaSvc;
        }

        /// <summary>
        /// Send the event for the configured external communications, for now it is Apache Kafka
        /// </summary>
        /// <param name="message">TradeEvent object</param>
        /// <returns></returns>
        public async Task<bool> SendEvent(TradeEvent message)
        {
            bool result = false;

            if(isKafkaEnabled)
            {
                // in a future avro (kafka), or anything related, this will be the layer responsible for the transformation of the object
                if (message.EventType == TradeEventType.ERROR)
                    _logger.LogError(message.Payload);
                else
                    _logger.LogInformation($"TradeEvent output: {message.Payload}");

                result = await _kafkaSvc.SendMessage(message);
            } 

            return result;
        }
    }
}
