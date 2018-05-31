
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TransferControl.Management
{
    public class Transaction
    {

        public List<Job> TargetJobs { get; set; }
        public string AdrNo { get; set; }
        public string NodeType { get; set; }
        public string Position { get; set; }
        public string Point { get; set; }
        public string Position2 { get; set; }
        public string Point2 { get; set; }
        public string Slot { get; set; }
        public string Slot2 { get; set; }
        public string Method { get; set; }
        public string Arm { get; set; }
        public string Arm2 { get; set; }
        public string Angle { get; set; }
        public string Value { get; set; }
        public string CommandType { get; set; }
        public string CommandEncodeStr { get; set; }
        public string Type { get; set; }
        public int Piority { get; set; }
        public string ScriptName { get; set; }
        public string FormName { get; set; }

        //逾時
        private System.Timers.Timer timeOutTimer = new System.Timers.Timer();
        ITransactionReport TimeOutReport;

        public class Command
        {
            //LoadPort
            public class LoadPortType
            {
                public const string Load = "Load";
                public const string Mapping = "Mapping";
                public const string MappingLoad = "MappingLoad";
                public const string Unload = "Unload";
                public const string MappingUnload = "MappingUnload";
                public const string GetMapping = "GetMapping";
                public const string GetLED = "GetLED";
                //public const string GetStatus = "GetStatus";
                public const string Reset = "Reset";
                public const string InitialPos = "InitialPos";
                public const string ForceInitialPos = "ForceInitialPos";
                public const string GetCount = "GetCount";
                public const string UnClamp = "UnClamp";
                public const string Clamp = "Clamp";
                public const string UnDock = "UnDock";
                public const string Dock = "Dock";
                public const string VacuumOFF = "VacuumOFF";
                public const string VacuumON = "VacuumON";
                public const string UnLatchDoor = "UnLatchDoor";
                public const string LatchDoor = "LatchDoor";
                public const string DoorClose = "DoorClose";
                public const string DoorOpen = "DoorOpen";
                public const string DoorUp = "DoorUp";
                public const string DoorDown = "DoorDown";
                public const string ReadVersion = "ReadVersion";
                public const string ReadStatus = "ReadState";
                public const string MapperWaitPosition = "MapperWaitPosition";
                public const string MapperStartPosition = "MapperStartPosition"; 
                public const string MapperArmRetracted = "MapperArmRetracted";
                public const string MapperArmStretch = "MapperArmStretch";
                public const string MappingDown = "MappingDown";
            }
            

            //Robot
            public class RobotType
            {
                public const string Get = "Get";
                public const string DoubleGet = "DoubleGet";
                public const string WaitBeforeGet = "WaitBeforeGet";
                public const string WaitBeforePut = "WaitBeforePut";
                public const string GetAfterWait = "GetAfterWait";
                public const string Put = "Put";
                public const string PutWithoutBack = "PutWithoutBack";
                public const string PutBack = "PutBack";
                public const string DoublePut = "DoublePut";
                public const string GetWait = "GetWait";
                public const string PutWait = "PutWait";
                public const string WaferHold = "WaferHold";
                public const string WaferRelease = "WaferRelease";
                public const string RobotHome = "RobotHome";
                public const string RobotOrginSearch = "RobotOrginSearch";
                public const string RobotServo = "RobotServo";
                public const string RobotMode = "RobotMode";
                public const string RobotWaferRelease = "RobotWaferRelease";
                public const string RobotSpeed = "RobotSpeed";
                public const string Reset = "Reset";
                public const string GetStatus = "GetStatus";
                public const string GetSpeed = "GetSpeed";
                public const string GetRIO = "GetRIO";
            }
            //Aligner
            public class AlignerType
            {
                public const string Align = "Align";
                public const string WaferHold = "WaferHold";
                public const string WaferRelease = "WaferRelease";
                public const string Retract = "Retract";
                public const string AlignerMode = "AlignerMode";
                public const string AlignerSpeed = "AlignerSpeed";
                public const string AlignerOrigin = "AlignerOrigin";
                public const string AlignerServo = "AlignerServo";
                public const string AlignerHome = "AlignerHome";
                public const string GetStatus = "GetStatus";
                public const string Reset = "Reset";
                public const string GetSpeed = "GetSpeed";
                public const string GetRIO = "GetRIO";
            }
            //OCR
            public class OCRType
            {
                public const string Read = "Read";
                public const string Online = "Online";
                public const string Offline = "Offline";
                public const string GetOnline = "GetOnline";
            }
        }

        public Transaction()
        {
            AdrNo = "";
            NodeType = "";
            Position = "";
            Point = "";
            Position2 = "";
            Point2 = "";
            Slot = "";
            Slot2 = "";
            Method = "";
            Arm = "";
            Arm2 = "";
            Angle = "";
            Value = "";
            CommandType = "";
            CommandEncodeStr = "";
            ScriptName = "";
            Type = "";
            FormName = "";
            TargetJobs = new List<Job>();

            timeOutTimer.Enabled = false;

            timeOutTimer.Interval = 10000;

            timeOutTimer.Elapsed += new System.Timers.ElapsedEventHandler(TimeOutMonitor);

        }

        public void SetTimeOut(int Timeout)
        {
            timeOutTimer.Interval = Timeout;
        }

        public void SetTimeOutMonitor(bool Enabled)
        {

            if (Enabled)
            {
                timeOutTimer.Start();
            }
            else
            {
                timeOutTimer.Stop();
            }

        }

        public void SetTimeOutReport(ITransactionReport _TimeOutReport)
        {
            TimeOutReport = _TimeOutReport;
        }

        private void TimeOutMonitor(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (TimeOutReport != null)
            {
                TimeOutReport.On_Transaction_TimeOut(this);
            }
        }
    }
}
