using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AtrapalhanciaWebSocket
{
    public interface IServiceBehaviorEventsInvoker
    {
        void InvokeOpenEvent();
        void InvokeMessageEvent(string message);
        void InvokeCloseEvent();
    }
}
