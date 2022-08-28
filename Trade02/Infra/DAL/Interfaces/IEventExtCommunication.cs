using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Trade02.Models.Trade;

namespace Trade02.Infra.DAL.Interfaces
{
    public interface IEventExtCommunication
    {
        public Task<bool> SendMessage(TradeEvent message);
        public Task<bool> SendMessages(List<string> messages);
    }
}
