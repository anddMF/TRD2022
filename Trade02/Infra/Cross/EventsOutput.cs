using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace Trade02.Infra.Cross
{
    public class EventsOutput : IEventsOutput
    {
        // send output
        private readonly ILogger<Worker> _logger;
        public EventsOutput(ILogger<Worker> logger)
        {
            _logger = logger;
        }

        public void SendEvent(string message)
        {
            throw new NotImplementedException();
        }
    }
}
