using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Trade02.Infra.DAL.Interfaces;
using Trade02.Models.Trade;

namespace Trade02.Infra.Cross
{
    public class EventsOutput : IEventsOutput
    {
        // send output
        private readonly ILogger _logger;
        private static IEventExtCommunication _kafkaSvc;
        public EventsOutput(ILogger<EventsOutput> logger, IEventExtCommunication kafkaSvc)
        {
            _logger = logger;
            _kafkaSvc = kafkaSvc;
        }

        /// <summary>
        /// Send the event for the configured external communications
        /// </summary>
        /// <param name="message">TradeEvent object</param>
        /// <returns></returns>
        public async Task<bool> SendEvent(TradeEvent message)
        {
            // in a future avro (kafka), or anything related, this will be the layer responsible for the transformation of the object
            if (message.EventType == TradeEventType.ERROR)
                _logger.LogError(message.Payload);
            else
                _logger.LogInformation($"TradeEvent output: {message.Payload}");

            bool result = await _kafkaSvc.SendMessage(message);

            return result;
        }
    }
}
