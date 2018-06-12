using log4net;
using Newtonsoft.Json;
using SANWA.Utility;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TransferControl.Controller;

namespace TransferControl.Management
{
    public class Node
    {




        ILog logger = LogManager.GetLogger(typeof(Node));
        public string Name { get; set; }
        public string Controller { get; set; }
        public string AdrNo { get; set; }
        public string Type { get; set; }
        public string Brand { get; set; }
        public string Phase { get; set; }
        public string CurrentLoadPort { get; set; }
        public string LockByNode { get; set; }
        public string CurrentPosition { get; set; }
        public string CurrentWaitNode { get; set; }
        public bool Enable { get; set; }
        public int WaitForCarryCount { get; set; }
        public bool Available { get; set; }
        public bool PutAvailable { get; set; }
        public bool GetAvailable { get; set; }
        public bool GetMutex { get; set; }
        public bool InterLock { get; set; }
        public string State { get; set; }
        public string StatusInfo { get; set; }
        public string Mode { get; set; }
        public bool Reserve { get; set; }
        public bool PutOut { get; set; }
        public string UnLockByJob { get; set; }
        public string PutOutArm { get; set; }
        public bool AllDone { get; set; }
        public bool InitialComplete { get; set; }
        public bool IsMapping { get; set; }
        public bool Fetchable { get; set; }
        public bool Release { get; set; }
        public bool HasAlarm { get; set; }
        public DateTime LoadTime { get; set; }
        public ConcurrentDictionary<string, Job> ReserveList { get; set; }
        public ConcurrentDictionary<string, Job> JobList { get; set; }
        public List<Route> RouteTable { get; set; }



        public class Route
        {
            public string NodeName { get; set; }
            public string NodeType { get; set; }
            public string Point { get; set; }
        }

        public void Initial()
        {
            JobList = new ConcurrentDictionary<string, Job>();
            ReserveList = new ConcurrentDictionary<string, Job>();
            Phase = "1";
            CurrentLoadPort = "";
            LockByNode = "";
            CurrentWaitNode = "";
            CurrentPosition = "";
            PutOutArm = "";
            UnLockByJob = "";
            State = "";
            StatusInfo = "";
            PutOut = false;
            PutAvailable = true;
            GetAvailable = true;
            GetMutex = true;
            InterLock = false;
            Reserve = false;
            AllDone = false;
            Available = true;
            Enable = true;
            Release = true;
            HasAlarm = false;
            if (Type == "LoadPort")
            {
                Available = false;
            }
            Fetchable = false;
            LoadTime = new DateTime();

        }

        public void ExcuteScript(string ScriptName, string FormName, bool Force = false)
        {
            CommandScript StartCmd = CommandScriptManagement.GetStart(ScriptName);
            if (StartCmd != null)
            {
                Transaction txn = new Transaction();
                txn.Method = StartCmd.Method;
                txn.FormName = FormName;
                txn.ScriptName = ScriptName;
                txn.Arm = StartCmd.Arm;
                txn.Position = StartCmd.Position;
                txn.Slot = StartCmd.Slot;
                txn.Value = StartCmd.Value;
                txn.ScriptIndex = StartCmd.Index;
                List<Job> dummyJob = new List<Job>();
                Job dummy = new Job();
                dummy.Job_Id = "dummy";
                dummyJob.Add(dummy);
                txn.TargetJobs = dummyJob;
                logger.Debug("Excute Script:" + ScriptName + " Method:" + txn.Method);
                SendCommand(txn, Force);
            }
        }

        public void ExcuteScript(string ScriptName, string FormName, Dictionary<string, string> Param)
        {
            CommandScriptManagement.ReloadScriptWithParam(ScriptName, Param);
            CommandScript StartCmd = CommandScriptManagement.GetStart(ScriptName);
            if (StartCmd != null)
            {
                Transaction txn = new Transaction();
                txn.Method = StartCmd.Method;
                txn.FormName = FormName;
                txn.ScriptName = ScriptName;
                txn.Arm = StartCmd.Arm;
                txn.Position = StartCmd.Position;
                txn.Slot = StartCmd.Slot;
                txn.Value = StartCmd.Value;
                txn.ScriptIndex = StartCmd.Index;
                List<Job> dummyJob = new List<Job>();
                Job dummy = new Job();
                dummy.Job_Id = "dummy";
                dummyJob.Add(dummy);
                txn.TargetJobs = dummyJob;
                logger.Debug("Excute Script:" + ScriptName + " Method:" + txn.Method);
                SendCommand(txn);
            }
        }

        public bool SendCommand(Transaction txn, bool Force = false)
        {
            //var watch = System.Diagnostics.Stopwatch.StartNew();


            bool result = false;
            try
            {

                IController Ctrl = ControllerManagement.Get(Controller);
                if (this.Brand.ToUpper().Equals("KAWASAKI"))
                {

                    txn.Seq = Ctrl.GetNextSeq();

                }
                else
                {
                    txn.Seq = "";
                }
                txn.AdrNo = AdrNo;
                txn.NodeName = this.Name;
                txn.NodeType = Type;
                if (txn.Value != null)
                {
                    if (!txn.Value.Equals(""))
                    {
                        CmdParamManagement.ParamMapping Mapping = CmdParamManagement.FindMapping(this.Brand.ToUpper(), txn.Method, "Value", txn.Value);
                        if (Mapping != null)
                        {
                            txn.Value = Mapping.MappingCode;
                        }
                    }
                }
                if (txn.Arm != null)
                {
                    if (!txn.Arm.Equals(""))
                    {
                        CmdParamManagement.ParamMapping Mapping = CmdParamManagement.FindMapping(this.Brand.ToUpper(), txn.Method, "Arm", txn.Arm);
                        if (Mapping != null)
                        {
                            txn.Arm = Mapping.MappingCode;
                        }
                    }
                }
                foreach (Route each in RouteTable)
                {
                    if (txn.Position.Equals(each.NodeName))
                    {
                        txn.Point = each.Point;
                        break;
                    }
                }
                switch (this.Type)
                {
                    case "LoadPort":
                        switch (txn.Method)
                        {
                            case Transaction.Command.LoadPortType.SetOpAccessBlink:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().LoadPort.Indicator(EncoderLoadPort.CommandType.Normal, EncoderLoadPort.IndicatorType.OpAccess, EncoderLoadPort.IndicatorStatus.Flashes);
                                break;
                            case Transaction.Command.LoadPortType.SetOpAccess:
                                if (txn.Value.Equals("1"))
                                {
                                    txn.CommandEncodeStr = Ctrl.GetEncoder().LoadPort.Indicator(EncoderLoadPort.CommandType.Normal, EncoderLoadPort.IndicatorType.OpAccess, EncoderLoadPort.IndicatorStatus.ON);
                                }
                                else
                                {
                                    txn.CommandEncodeStr = Ctrl.GetEncoder().LoadPort.Indicator(EncoderLoadPort.CommandType.Normal, EncoderLoadPort.IndicatorType.OpAccess, EncoderLoadPort.IndicatorStatus.OFF);
                                }
                                break;
                            case Transaction.Command.LoadPortType.GetLED:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().LoadPort.LEDIndicatorStatus();
                                break;
                            case Transaction.Command.LoadPortType.GetMapping:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().LoadPort.WaferSorting(EncoderLoadPort.MappingSortingType.Asc);
                                break;
                            case Transaction.Command.LoadPortType.ReadStatus:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().LoadPort.Status();
                                break;
                            case Transaction.Command.LoadPortType.InitialPos:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().LoadPort.InitialPosition(EncoderLoadPort.CommandType.Normal);
                                break;
                            case Transaction.Command.LoadPortType.ForceInitialPos:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().LoadPort.ForcedInitialPosition(EncoderLoadPort.CommandType.Normal);
                                break;
                            case Transaction.Command.LoadPortType.Load:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().LoadPort.Load(EncoderLoadPort.CommandType.Normal);
                                break;
                            case Transaction.Command.LoadPortType.MappingLoad:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().LoadPort.MappingLoad(EncoderLoadPort.CommandType.Normal);
                                break;
                            case Transaction.Command.LoadPortType.MappingUnload:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().LoadPort.MapAndUnload(EncoderLoadPort.CommandType.Normal);
                                break;
                            case Transaction.Command.LoadPortType.Reset:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().LoadPort.Reset(EncoderLoadPort.CommandType.Normal);
                                break;
                            case Transaction.Command.LoadPortType.Unload:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().LoadPort.Unload(EncoderLoadPort.CommandType.Normal);
                                break;
                            case Transaction.Command.LoadPortType.Mapping:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().LoadPort.MappingInLoad(EncoderLoadPort.CommandType.Normal);
                                break;
                            case Transaction.Command.LoadPortType.GetCount:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().LoadPort.WaferQuantity();
                                break;
                            case Transaction.Command.LoadPortType.UnClamp:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().LoadPort.FOUPClampRelease(EncoderLoadPort.CommandType.Normal);
                                break;
                            case Transaction.Command.LoadPortType.Clamp:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().LoadPort.FOUPClampFix(EncoderLoadPort.CommandType.Normal);
                                break;
                            case Transaction.Command.LoadPortType.Dock:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().LoadPort.Dock(EncoderLoadPort.CommandType.Normal);
                                break;
                            case Transaction.Command.LoadPortType.UnDock:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().LoadPort.Undock(EncoderLoadPort.CommandType.Normal);
                                break;
                            case Transaction.Command.LoadPortType.VacuumOFF:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().LoadPort.VacuumOFF(EncoderLoadPort.CommandType.Normal);
                                break;
                            case Transaction.Command.LoadPortType.VacuumON:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().LoadPort.VacuumON(EncoderLoadPort.CommandType.Normal);
                                break;
                            case Transaction.Command.LoadPortType.UnLatchDoor:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().LoadPort.LatchkeyRelease(EncoderLoadPort.CommandType.Normal);
                                break;
                            case Transaction.Command.LoadPortType.LatchDoor:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().LoadPort.LatchkeyFix(EncoderLoadPort.CommandType.Normal);
                                break;
                            case Transaction.Command.LoadPortType.DoorClose:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().LoadPort.DoorClose(EncoderLoadPort.CommandType.Normal);
                                break;
                            case Transaction.Command.LoadPortType.DoorOpen:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().LoadPort.DoorOpen(EncoderLoadPort.CommandType.Normal);
                                break;
                            case Transaction.Command.LoadPortType.DoorDown:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().LoadPort.DoorDown(EncoderLoadPort.CommandType.Normal);
                                break;
                            case Transaction.Command.LoadPortType.DoorUp:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().LoadPort.DoorUp(EncoderLoadPort.CommandType.Normal);
                                break;
                            case Transaction.Command.LoadPortType.ReadVersion:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().LoadPort.Version();
                                break;
                            case Transaction.Command.LoadPortType.MapperWaitPosition:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().LoadPort.MapperWaitPosition(EncoderLoadPort.CommandType.Normal);
                                break;
                            case Transaction.Command.LoadPortType.MapperStartPosition:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().LoadPort.MapperStartPosition(EncoderLoadPort.CommandType.Normal);
                                break;
                            case Transaction.Command.LoadPortType.MapperArmRetracted:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().LoadPort.MapperArmClose(EncoderLoadPort.CommandType.Normal);
                                break;
                            case Transaction.Command.LoadPortType.MapperArmStretch:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().LoadPort.MapperArmOpen(EncoderLoadPort.CommandType.Normal);
                                break;
                            case Transaction.Command.LoadPortType.MappingDown:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().LoadPort.MappingDown(EncoderLoadPort.CommandType.Normal);
                                break;
                        }
                        break;
                    case "Robot":
                        switch (txn.Method)
                        {

                            case Transaction.Command.RobotType.GetStatus:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().Robot.Status(AdrNo, txn.Seq);
                                break;
                            case Transaction.Command.RobotType.GetCombineStatus:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().Robot.CombinedStatus(AdrNo, txn.Seq);
                                break;
                            case Transaction.Command.RobotType.GetSpeed:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().Robot.Speed(AdrNo, txn.Seq);
                                break;
                            case Transaction.Command.RobotType.GetRIO:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().Robot.StatusIO(AdrNo, txn.Seq, txn.Value);
                                break;
                            case Transaction.Command.RobotType.GetSV:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().Robot.SolenoidValve(AdrNo, txn.Seq, txn.Value);
                                break;
                            case Transaction.Command.RobotType.Stop:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().Robot.DeviceStop(AdrNo, txn.Seq, txn.Value);
                                break;
                            case Transaction.Command.RobotType.Pause:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().Robot.DevicePause(AdrNo, txn.Seq);
                                break;
                            case Transaction.Command.RobotType.Continue:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().Robot.DeviceContinue(AdrNo, txn.Seq);
                                break;
                            case Transaction.Command.RobotType.GetMode:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().Robot.GetMode(AdrNo, txn.Seq);
                                break;
                            case Transaction.Command.RobotType.GetError:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().Robot.ErrorMessage(AdrNo, txn.Seq, txn.Value);
                                break;
                            case Transaction.Command.RobotType.Get:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().Robot.GetWafer(AdrNo, txn.Seq, txn.Arm, txn.Point, "0", txn.Slot);
                                break;
                            case Transaction.Command.RobotType.Put:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().Robot.PutWafer(AdrNo, txn.Seq, txn.Arm, txn.Point, txn.Slot);
                                break;
                            case Transaction.Command.RobotType.DoubleGet:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().Robot.GetWafer(AdrNo, txn.Seq, "3", txn.Point, "0", txn.Slot);
                                break;
                            case Transaction.Command.RobotType.DoublePut:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().Robot.PutWafer(AdrNo, txn.Seq, "3", txn.Point, txn.Slot);
                                break;
                            case Transaction.Command.RobotType.WaitBeforeGet:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().Robot.GetWaferToStandBy(AdrNo, txn.Seq, txn.Arm, txn.Point, "0", txn.Slot);
                                break;
                            case Transaction.Command.RobotType.WaitBeforePut:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().Robot.PutWaferToStandBy(AdrNo, txn.Seq, txn.Arm, txn.Point, txn.Slot);
                                break;
                            case Transaction.Command.RobotType.GetAfterWait:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().Robot.GetWaferToContinue(AdrNo, txn.Seq, txn.Arm, txn.Point, "0", txn.Slot);
                                break;
                            case Transaction.Command.RobotType.PutWithoutBack:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().Robot.PutWaferToDown(AdrNo, txn.Seq, txn.Arm, txn.Point, txn.Slot);
                                break;
                            case Transaction.Command.RobotType.PutBack:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().Robot.PutWaferToContinue(AdrNo, txn.Seq, txn.Arm, txn.Point, txn.Slot);
                                break;
                            case Transaction.Command.RobotType.GetWait:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().Robot.GetWaferToReady(AdrNo, txn.Seq, txn.Arm, txn.Point, txn.Slot);
                                break;
                            case Transaction.Command.RobotType.PutWait:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().Robot.PutWaferToReady(AdrNo, txn.Seq, txn.Arm, txn.Point, txn.Slot);
                                break;
                            case Transaction.Command.RobotType.RobotHome:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().Robot.Home(AdrNo, txn.Seq);
                                break;
                            case Transaction.Command.RobotType.RobotHomeSafety:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().Robot.HomeSafety(AdrNo, txn.Seq);
                                break;
                            case Transaction.Command.RobotType.RobotOrginSearch:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().Robot.OrginSearch(AdrNo, txn.Seq);
                                break;
                            case Transaction.Command.RobotType.WaferRelease:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().Robot.WaferReleaseHold(AdrNo, txn.Seq, txn.Arm);
                                break;
                            case Transaction.Command.RobotType.WaferHold:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().Robot.WaferHold(AdrNo, txn.Seq, txn.Arm);
                                break;
                            case Transaction.Command.RobotType.RobotServo:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().Robot.ServoOn(AdrNo, txn.Seq, txn.Value);
                                break;
                            case Transaction.Command.RobotType.RobotMode:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().Robot.Mode(AdrNo, txn.Seq, txn.Value);
                                break;
                            case Transaction.Command.RobotType.Reset:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().Robot.ErrorReset(AdrNo, txn.Seq);
                                break;
                            case Transaction.Command.RobotType.RobotSpeed:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().Robot.setSpeed(AdrNo, txn.Seq, txn.Value);
                                break;
                        }
                        break;
                    case "Aligner":
                        switch (txn.Method)
                        {
                            case Transaction.Command.AlignerType.GetStatus:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().Aligner.Status(AdrNo, "");
                                break;
                            case Transaction.Command.AlignerType.GetSpeed:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().Aligner.Speed(AdrNo, "");
                                break;
                            case Transaction.Command.AlignerType.GetRIO:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().Aligner.StatusIO(AdrNo, "", txn.Value);
                                break;
                            case Transaction.Command.AlignerType.Stop:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().Aligner.DeviceStop(AdrNo, "", txn.Value);
                                break;
                            case Transaction.Command.AlignerType.Pause:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().Aligner.DevicePause(AdrNo, "");
                                break;
                            case Transaction.Command.AlignerType.Continue:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().Aligner.DeviceContinue(AdrNo, "");
                                break;
                            case Transaction.Command.AlignerType.GetSV:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().Aligner.SolenoidValve(AdrNo, "", txn.Value);
                                break;
                            case Transaction.Command.AlignerType.GetError:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().Aligner.ErrorMessage(AdrNo, "", txn.Value);
                                break;
                            case Transaction.Command.AlignerType.GetMode:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().Aligner.GetMode(AdrNo, "");
                                break;
                            case Transaction.Command.AlignerType.AlignerHome:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().Aligner.Home(AdrNo, "");
                                break;
                            case Transaction.Command.AlignerType.Align:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().Aligner.Align(AdrNo, "", txn.Value);
                                break;
                            case Transaction.Command.AlignerType.Retract:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().Aligner.Retract(AdrNo, "");
                                break;
                            case Transaction.Command.AlignerType.WaferRelease:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().Aligner.WaferReleaseHold(AdrNo, "", txn.Arm);
                                break;
                            case Transaction.Command.AlignerType.WaferHold:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().Aligner.WaferHold(AdrNo, "");
                                break;
                            case Transaction.Command.AlignerType.AlignerServo:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().Aligner.ServoOn(AdrNo, "", txn.Value);
                                break;
                            case Transaction.Command.AlignerType.AlignerMode:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().Aligner.Mode(AdrNo, "", txn.Value);
                                break;
                            case Transaction.Command.AlignerType.AlignerOrigin:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().Aligner.OrginSearch(AdrNo, "");
                                break;
                            case Transaction.Command.AlignerType.Reset:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().Aligner.ErrorReset(AdrNo, "");
                                break;
                            case Transaction.Command.AlignerType.AlignerSpeed:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().Aligner.setSpeed(AdrNo, "", txn.Value);
                                break;

                        }
                        break;
                    case "OCR":
                        switch (txn.Method)
                        {
                            case Transaction.Command.OCRType.GetOnline:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().OCR.GetOnline();
                                txn.CommandType = "";
                                break;
                            case Transaction.Command.OCRType.Online:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().OCR.SetOnline(EncoderOCR.OnlineStatus.Online);
                                txn.CommandType = "";
                                break;
                            case Transaction.Command.OCRType.Offline:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().OCR.SetOnline(EncoderOCR.OnlineStatus.Offline);
                                txn.CommandType = "";
                                break;
                            case Transaction.Command.OCRType.Read:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().OCR.Read();
                                txn.CommandType = "CMD";
                                break;
                        }
                        break;
                }

                if (this.Type.Equals("Robot"))
                {
                    if (txn.TargetJobs.Count == 0)
                    {
                        Job tmp;
                        switch (txn.Arm)
                        {
                            case "1":
                                if (this.JobList.TryGetValue("1", out tmp))
                                {
                                    txn.TargetJobs.Add(tmp);
                                }
                                break;
                            case "2":
                                if (this.JobList.TryGetValue("2", out tmp))
                                {
                                    txn.TargetJobs.Add(tmp);
                                }
                                break;
                            case "3":
                                if (this.JobList.TryGetValue("1", out tmp))
                                {
                                    txn.TargetJobs.Add(tmp);
                                }
                                if (this.JobList.TryGetValue("2", out tmp))
                                {
                                    txn.TargetJobs.Add(tmp);
                                }
                                break;
                        }
                    }
                }
                Job TargetJob;
                if (txn.TargetJobs != null)
                {
                    if (txn.TargetJobs.Count != 0)
                    {
                        TargetJob = txn.TargetJobs[0];
                    }
                    else
                    {
                        TargetJob = new Job();
                    }
                }
                else
                {
                    TargetJob = new Job();
                }
                if (txn.CommandType.Equals("CMD") || txn.CommandType.Equals("MOV"))
                {
                    if ((this.InterLock || !(this.UnLockByJob.Equals(TargetJob.Job_Id) || this.UnLockByJob.Equals(""))) && !Force)
                    {
                        ReturnMessage tmp = new ReturnMessage();
                        tmp.Value = "Interlock!";
                        logger.Error(this.Name + " Interlock! Txn:" + JsonConvert.SerializeObject(txn));
                        ControllerManagement.Get(Controller)._ReportTarget.On_Command_Error(this, txn, tmp);

                        return false;
                    }
                }
                if (Ctrl.DoWork(txn))
                {

                    result = true;
                }
                else
                {
                    logger.Debug("SendCommand fail.");
                    result = false;
                }


            }
            catch (Exception e)
            {
                logger.Error("SendCommand " + e.Message + "\n" + e.StackTrace);
            }
            //watch.Stop();
            //var elapsedMs = watch.ElapsedMilliseconds;
            //logger.Info("SendCommand ProcessTime:"+ elapsedMs.ToString());
            return result;
        }

        public Job GetJob(string Slot)
        {
            Job result = null;

            //lock (JobList)
            //{
            JobList.TryGetValue(Slot, out result);
            //}

            return result;
        }
        public bool AddJob(string Slot, Job Job)
        {
            bool result = false;
            //lock (JobList)
            //{
            if (!JobList.ContainsKey(Slot))
            {
                JobList.TryAdd(Slot, Job);
                result = true;
            }
            //}
            return result;
        }

        public bool RemoveJob(string Slot)
        {
            bool result = false;
            //lock (JobList)
            //{
            if (JobList.ContainsKey(Slot))
            {
                Job tmp;
                JobList.TryRemove(Slot, out tmp);
                result = true;
            }
            //}
            return result;
        }

        public Job GetReserve(string Slot)
        {
            Job result = null;

            //lock (JobList)
            //{
            ReserveList.TryGetValue(Slot, out result);
            //}

            return result;
        }
        public bool AddReserve(string Slot, Job Job)
        {
            bool result = false;
            //lock (JobList)
            //{
            if (!ReserveList.ContainsKey(Slot))
            {
                ReserveList.TryAdd(Slot, Job);
                result = true;
            }
            //}
            return result;
        }

        public bool RemoveReserve(string Slot)
        {
            bool result = false;
            //lock (JobList)
            //{
            if (ReserveList.ContainsKey(Slot))
            {
                Job tmp;
                ReserveList.TryRemove(Slot, out tmp);
                result = true;
            }
            //}
            return result;
        }

    }
}
