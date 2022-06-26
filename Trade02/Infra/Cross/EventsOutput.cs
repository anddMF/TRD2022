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
        private static IKafkaCommunication _kafkaSvc;
        public EventsOutput(ILogger<EventsOutput> logger, IKafkaCommunication kafkaSvc)
        {
            _logger = logger;
            _kafkaSvc = kafkaSvc;
        }

        public async Task<bool> SendEvent(string message)
        {
            _logger.LogInformation(message);
            bool result = await _kafkaSvc.SendMessage(message);

            return result;
        }
    }
}
