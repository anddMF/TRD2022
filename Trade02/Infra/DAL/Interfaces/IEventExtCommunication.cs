using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Trade02.Infra.DAL.Interfaces
{
    public interface IEventExtCommunication
    {
        public Task<bool> SendMessage(string message);
        public Task<bool> SendMessages(List<string> messages);
    }
}
