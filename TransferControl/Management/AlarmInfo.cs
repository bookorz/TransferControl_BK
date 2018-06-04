using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TransferControl.Management
{
    public class AlarmInfo
    {
        public string NodeName { get; set; }
        public string SystemAlarmCode { get; set; }
        public string AlarmType { get; set; }
        public string AlarmCode { get; set; }
        public string Desc { get; set; }
        public string EngDesc { get; set; }
        public string TimeStamp { get; set; }
    }
}
