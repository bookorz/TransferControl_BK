using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TransferControl.Management
{
    public class TransferRequest
    {
        public Node ExcuteNode { get; set; }
        public Transaction.Command ExcuteCmd { get; set; }
        public Node TargetNode { get; set; }
        public string TargetSlot { get; set; }
        public List<Job> TargetJobs { get; set; }
        public int Piority { get; set; }
    }
}
