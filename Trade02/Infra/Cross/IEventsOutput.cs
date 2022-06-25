using System;
using System.Collections.Generic;
using System.Text;

namespace Trade02.Infra.Cross
{
    public interface IEventsOutput
    {
        public void SendEvent(string message);
    }
}
