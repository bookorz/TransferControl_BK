using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TransferControl.Management
{
    public class Path
    {
        public class Action
        {
            public string EqpType { get; set; }
            public string Method { get; set; }
            public string Position { get; set; }
            public string Param { get; set; }
            public bool Force { get; set; }
        }
        public string ID { get; set; }
        public string Expression { get; set; }
        public string JobStatus { get; set; }
        public string ExcuteMethod { get; set; }
        public string FinishMethod { get; set; }
        public string ChangeToStatus { get; set; }
        public List<Action> TodoList { get; set; }

        public Path()
        {
            this.ID = "";
        }
    }
}
