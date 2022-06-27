using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Trade02.Models.Trade;

namespace Trade02.Infra.Cross
{
    public interface IEventsOutput
    {
        public Task<bool> SendEvent(TradeEvent message);
    }
}
