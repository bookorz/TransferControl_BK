using TransferControl.Management;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TransferControl.Controller
{
    public interface IController
    {
        //void Connect();
        //void Close();
        void Start();
        bool DoWork(Transaction Txn);
        string GetNextSeq();
        SANWA.Utility.Encoder GetEncoder();
    }
}
