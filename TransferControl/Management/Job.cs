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
        public bool NeedProcess { get; set; }
        public bool ProcessFlag { get; set; }
        public bool AlignerFlag { get; set; }
        public bool OCRFlag { get; set; }
        public bool AlignerFinished { get; set; }
        public bool OCRFinished { get; set; }
        public string OCRImgPath { get; set; }
        public string OCRScore { get; set; }
        public string Position { get; set; }
        public string FromPort { get; set; }
        public string FromPortSlot { get; set; }
        public string Destination { get;  private set ; }
        public string DisplayDestination { get; private set; }
        public string DestinationSlot { get; private set; }
        public string LastNode { get; set; }
        public string LastSlot { get; set; }
        public string CurrentState { get; set; }
        public string WaitToDo { get; set; }
        public string ProcessNode { get; set; }
        public bool MapFlag { get; set; }
        public int Offset { get; set; }
        public int Angle { get; set; }
        public DateTime AssignTime { get; private set; }

        public Job()
        {
            Job_Id = "";
            WaitToDo = "";
            Destination = "";
            DestinationSlot = "";
            OCRImgPath = "";
            ProcessFlag = false;
            MapFlag = false;
            Angle = 270;
            AlignerFlag = true;
            NeedProcess = false;
            OCRFlag = true;
        }

        public void AssignPort(string Position , string Slot)
        {
            this.Destination = Position;
            this.DisplayDestination = Position.Replace("Load","");
            this.DestinationSlot = Slot;
            this.AssignTime = DateTime.Now;
        }

        public void UnAssignPort()
        {
            this.Destination = "";
            this.DisplayDestination = "";
            this.DestinationSlot = "";
            
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
