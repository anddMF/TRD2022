using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Trade02.Models.Trade;

namespace Trade02.Infra.Cross
{
    public interface IEventsOutput
    {
        /// <summary>
        /// Send the event for the configured external communications
        /// </summary>
        /// <param name="message">TradeEvent object</param>
        /// <returns></returns>
        public Task<bool> SendEvent(TradeEvent message);
    }
}
