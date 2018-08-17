using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TransferControl.Comm
{
    interface IConnection
    {
        bool Send(object Message);
        void Start();
    }
}
