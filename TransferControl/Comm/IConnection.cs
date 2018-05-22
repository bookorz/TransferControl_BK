using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TransferControl.Comm
{
    interface IConnection
    {
        void Connect();
        void Send(object Message);
        void Close();
    }
}
