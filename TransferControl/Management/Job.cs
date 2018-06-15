using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TransferControl.Management
{
    public class Job
    {
        public string Slot { get; set; }
        public string Job_Id { get; set; }
        public string Host_Job_Id { get; set; }
        public bool ProcessFlag { get; set; }
        public int Piority { get; set; }
        public bool AlignerFlag { get; set; }
        public bool OCRFlag { get; set; }
        public bool AlignerFinished { get; set; }
        public bool OCRFinished { get; set; }
        public string Position { get; set; }
        public string FromPort { get; set; }
        public string Destination { get; set; }
        public string DisplayDestination { get; set; }
        public string DestinationSlot { get; set; }
        public string LastNode { get; set; }
        public string CurrentState { get; set; }
        public string WaitToDo { get; set; }
        public string FetchRobot { get; set; }
        public string ProcessNode { get; set; }
        public bool MapFlag { get; set; }

        public Job()
        {
            Job_Id = "";
            WaitToDo = "";
            Destination = "";
            DestinationSlot = "";
            ProcessFlag = true;
            MapFlag = false;
        }

        public class State
        {
            public const string WAIT_PUT = "WAIT_PUT";
            public const string WAIT_WHLD = "WAIT_WHLD";
            public const string WAIT_ALIGN = "WAIT_ALIGN";
            public const string WAIT_OCR = "WAIT_OCR";
            public const string WAIT_WRLS = "WAIT_WRLS";
            public const string WAIT_GET = "WAIT_GET";
            public const string WAIT_RET = "WAIT_RET";
            public const string WAIT_UNLOAD = "WAIT_UNLOAD";
        }
    }
}
