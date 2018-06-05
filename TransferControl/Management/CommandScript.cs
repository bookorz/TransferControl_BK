using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TransferControl.Management
{
    public class CommandScript
    {
        public string ExcuteMethod { get; set; }
        public string FinishMethod { get; set; }
        public string Method { get; set; }
        public string Arm { get; set; }
        public string Slot { get; set; }
        public string Position { get; set; }
        public string Value { get; set; }
        public string Flag { get; set; }
    }
}
