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
        public string Type { get; set; }
        public bool IsStop { get; set; }
        public bool NeedReset { get; set; }
        public DateTime TimeStamp { get; set; }
        public AlarmInfo()
        {
            NodeName = "";
            SystemAlarmCode = "";
            AlarmType = "";
            AlarmCode = "";
            Desc = "";
            EngDesc = "";
            Type = "";
            IsStop = false;
            NeedReset = false;
          
        }
    }
}
