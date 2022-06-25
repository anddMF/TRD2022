using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Trade02.Infra.Cross
{
    public interface IEventsOutput
    {
        public Task<bool> SendEvent(string message);
    }
}
