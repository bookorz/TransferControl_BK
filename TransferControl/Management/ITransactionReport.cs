using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TransferControl.Management
{
    public interface ITransactionReport
    {
        void On_Transaction_TimeOut(Transaction Txn);
    }
}
