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
        /// <summary>
        /// 名稱
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// 目前使用的Controoler名稱
        /// </summary>
        public string Controller { get; set; }
        /// <summary>
        /// Address Number
        /// </summary>
        public string AdrNo { get; set; }
        /// <summary>
        /// Node 類型
        /// </summary>
        public string Type { get; set; }
        /// <summary>
        /// 廠牌
        /// </summary>
        public string Brand { get; set; }
        /// <summary>
        /// LoadPort專用，目前取片中
        /// </summary>
        public bool Used { get; set; }
        /// <summary>
        /// Robot專用，搬送階段
        /// </summary>
        public string Phase { get; set; }
        /// <summary>
        /// 目前Foup的ID
        /// </summary>
        public string FoupID { get; set; }
        /// <summary>
        /// Control Job ID
        /// </summary>
        public string CjID { get; set; }
        /// <summary>
        /// Process Request ID
        /// </summary>
        public string PrID { get; set; }
        /// <summary>
        /// Robot專用，取片階段用於標記Foup
        /// </summary>
        public string CurrentLoadPort { get; set; }
        /// <summary>
        /// Robot專用，目前手臂的位置
        /// </summary>
        public string CurrentPosition { get; set; }
        /// <summary>
        /// 啟用或停用此Node
        /// </summary>
        public bool Enable { get; set; }
        /// <summary>
        /// Robot專用，未來需要此Robot，還沒有搬送的數量
        /// </summary>
        public int WaitForCarryCount { get; set; }
        /// <summary>
        /// LoadPort用於標記準備完成狀態，Aligner用於標記可否放片
        /// </summary>
        public bool Available { get; set; }
        /// <summary>
        /// 處理階段用於放片鎖定
        /// </summary>
        public bool PutAvailable { get; set; }
        /// <summary>
        /// 處理階段用於標記Robot可否去Aligner取片
        /// </summary>
        public bool GetAvailable { get; set; }
        /// <summary>
        /// 鎖定Robot不能執行其他命令
        /// </summary>
        public bool GetMutex { get; set; }
        /// <summary>
        /// LoadPort用於標記True為目前不能取放片，其他裝置用於標記True為正在執行命令中
        /// </summary>
        public bool InterLock { get; set; }
        /// <summary>
        /// 目前機況
        /// </summary>
        public string State { get; set; }
        /// <summary>
        /// 上一次的機況
        /// </summary>
        public string LastState { get; set; }
        /// <summary>
        /// LoadPort專用，標記LD/UD/LU
        /// </summary>
        public string Mode { get; set; }
        /// <summary>
        /// 標記是否被預約執行命令中
        /// </summary>
        public bool Reserve { get; set; }
        /// <summary>
        /// 手臂伸出中
        /// </summary>
        public bool PutOut { get; set; }
        /// <summary>
        /// 由Wafer鎖定直到完成該Wafer所要求的命令
        /// </summary>
        public string UnLockByJob { get; set; }
        /// <summary>
        /// 紀錄伸出的是哪支手臂
        /// </summary>
        public string PutOutArm { get; set; }
        /// <summary>
        /// 手臂上的Wafer都做完了
        /// </summary>
        public bool AllDone { get; set; }
        /// <summary>
        /// 是否需要Initial
        /// </summary>
        public bool InitialComplete { get; set; }
        /// <summary>
        /// LoadPort專用，Mapping完成
        /// </summary>
        public bool IsMapping { get; set; }
        /// <summary>
        /// LoadPort專用，目前可以取片
        /// </summary>
        public bool Fetchable { get; set; }
        /// <summary>
        /// 標記為虛擬裝置
        /// </summary>
        public bool ByPass { get; set; }
        /// <summary>
        /// LoadPort專用，標記已經按過OP按鈕
        /// </summary>
        public bool FoupReady { get; set; }
        /// <summary>
        /// LoadPort專用，紀錄Demo模式Wafer所指定的目的地Foup
        /// </summary>
        public string DestPort { get; set; }
        /// <summary>
        /// Robot專用，預設使用的Aligner
        /// </summary>
        public string DefaultAligner { get; set; }
        /// <summary>
        /// Robot專用，次要使用的Aligner
        /// </summary>
        public string AlternativeAligner { get; set; }
        /// <summary>
        /// LoadPort專用，Foup Load的時間
        /// </summary>
        public DateTime LoadTime { get; set; }
        /// <summary>
        /// LoadPort專用，其他Port Assign用
        /// </summary>
        public ConcurrentDictionary<string, Job> ReserveList { get; set; }
        /// <summary>
        /// 在席列表
        /// </summary>
        public ConcurrentDictionary<string, Job> JobList { get; set; }

        //Demo用Condition
        public bool PortUnloadAndLoadFinished { get; set; }

        public bool Busy { get; set; }

        public string LastFinMethod { get; set; }

        public bool WaitForFinish { get; set; }

        public string WaferSize { get; set; }

        public bool DoubleArmActive { get; set; }

        public bool HasPresent { get; set; }

        public bool CheckStatus { get; set; }

        public bool IsWaferHold { get; set; }

        public string ErrorMsg { get; set; }

        public void InitialObject()
        {
            JobList = new ConcurrentDictionary<string, Job>();
            ReserveList = new ConcurrentDictionary<string, Job>();
            Phase = "1";
            if (Type == "Aliger")
            {
                Phase = "2";
            }
            CurrentLoadPort = "";
            FoupID = "";
            PrID = "";
            CjID = "";
            CurrentPosition = "";
            PutOutArm = "";
            UnLockByJob = "";
            State = "Idle";
            if (Type.Equals("LOADPORT"))
            {
                State = "Ready To Load";
            }
            LastState = "Idle";
            LastFinMethod = "";
            Busy = false;
            PutOut = false;
            PutAvailable = true;
            GetAvailable = true;
            GetMutex = true;
            InterLock = false;
            Reserve = false;
            AllDone = false;
            Available = true;
            HasPresent = false;
            CheckStatus = false;
            WaitForFinish = false;
            InitialComplete = false;
            IsWaferHold = false;

            ErrorMsg = "";
            //Enable = true;

            Used = false;

            if (Type.Equals("LOADPORT"))
            {
                Available = false;
                Mode = "UD";
            }
            Fetchable = false;
            FoupReady = false;
            DestPort = "";
            LoadTime = new DateTime();
            PortUnloadAndLoadFinished = false;
        }
        /// <summary>
        /// 執行命令腳本
        /// </summary>
        /// <param name="ScriptName"></param>
        /// <param name="FormName"></param>
        /// <param name="Force"></param>
        public void ExcuteScript(string ScriptName, string FormName, string RecipeID="", bool Force = false)
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
                txn.RecipeID = RecipeID;
                //List<Job> dummyJob = new List<Job>();
                //Job dummy = new Job();
                //dummy.Job_Id = "dummy";
                //dummyJob.Add(dummy);
                //txn.TargetJobs = dummyJob;
                logger.Debug("Excute Script:" + ScriptName + " Method:" + txn.Method);
                SendCommand(txn, Force);
            }
        }
        /// <summary>
        /// 執行命令腳本(帶參數)
        /// </summary>
        /// <param name="ScriptName"></param>
        /// <param name="FormName"></param>
        /// <param name="Force"></param>
        public void ExcuteScript(string ScriptName, string FormName, Dictionary<string, string> Param, string RecipeID = "")
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
                txn.RecipeID = RecipeID;
                //List<Job> dummyJob = new List<Job>();
                //Job dummy = new Job();
                //dummy.Job_Id = "dummy";
                //dummyJob.Add(dummy);
                //txn.TargetJobs = dummyJob;
                logger.Debug("Excute Script:" + ScriptName + " Method:" + txn.Method);
                SendCommand(txn);
            }
        }
        /// <summary>
        /// 傳送命令
        /// </summary>
        /// <param name="txn"></param>
        /// <param name="Force"></param>
        /// <returns></returns>
        public bool SendCommand(Transaction txn, bool Force = false)
        {
            //var watch = System.Diagnostics.Stopwatch.StartNew();


            bool result = false;
            try
            {
                if (this.ByPass)
                {
                    
                        logger.Debug("Command cancel,Cause " + this.Name + " in by pass mode.");
                        return true;
                    
                }

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
                
                if (!txn.Position.Equals(""))
                {
                    if (txn.RecipeID.Equals(""))
                    {
                        if (txn.TargetJobs.Count != 0)
                        {
                            txn.RecipeID = txn.TargetJobs[0].RecipeID;
                        }
                        else if (!NodeManagement.Get(txn.Position).WaferSize.Equals(""))
                        {
                            txn.RecipeID = NodeManagement.Get(txn.Position).WaferSize;
                        }
                    }
                    RobotPoint point;
                    if (txn.Method.Equals(Transaction.Command.RobotType.Mapping))
                    {
                        point = PointManagement.GetMapPoint(txn.Position, txn.RecipeID);
                    }
                    else
                    {
                        point = PointManagement.GetPoint(Name, txn.Position, txn.RecipeID);
                    }
                    if (point == null)
                    {
                        logger.Error("point " + txn.Position + " not found!");
                        return false;
                    }

                    txn.Point = point.Point;
                    if (point.PositionType.Equals("LOADPORT"))
                    {
                        Node port = NodeManagement.Get(point.Position);
                        if (port != null)
                        {
                            if (!port.ByPass)
                            {
                                Transaction InterLockTxn = new Transaction();
                                InterLockTxn.Method = Transaction.Command.LoadPortType.ReadStatus;
                                InterLockTxn.FormName = "InterLockChk";
                                port.SendCommand(InterLockTxn);
                            }
                        }
                    }
                }

                switch (this.Type)
                {
                    case "LOADPORT":
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
                            case Transaction.Command.LoadPortType.SetLoad:
                                if (txn.Value.Equals("1"))
                                {
                                    txn.CommandEncodeStr = Ctrl.GetEncoder().LoadPort.Indicator(EncoderLoadPort.CommandType.Normal, EncoderLoadPort.IndicatorType.Load, EncoderLoadPort.IndicatorStatus.ON);
                                }
                                else
                                {
                                    txn.CommandEncodeStr = Ctrl.GetEncoder().LoadPort.Indicator(EncoderLoadPort.CommandType.Normal, EncoderLoadPort.IndicatorType.Load, EncoderLoadPort.IndicatorStatus.OFF);
                                }
                                break;
                            case Transaction.Command.LoadPortType.SetUnLoad:
                                if (txn.Value.Equals("1"))
                                {
                                    txn.CommandEncodeStr = Ctrl.GetEncoder().LoadPort.Indicator(EncoderLoadPort.CommandType.Normal, EncoderLoadPort.IndicatorType.Unload, EncoderLoadPort.IndicatorStatus.ON);
                                }
                                else
                                {
                                    txn.CommandEncodeStr = Ctrl.GetEncoder().LoadPort.Indicator(EncoderLoadPort.CommandType.Normal, EncoderLoadPort.IndicatorType.Unload, EncoderLoadPort.IndicatorStatus.OFF);
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
                    case "ROBOT":
                        switch (txn.Method)
                        {
                            case Transaction.Command.RobotType.GetMapping:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().Robot.MapList(AdrNo, txn.Seq, txn.Value);
                                break;
                            case Transaction.Command.RobotType.Mapping:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().Robot.Mapping(AdrNo, txn.Seq,txn.Point,"1",txn.Slot);
                                break;
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
                            case Transaction.Command.RobotType.RobotHomeA:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().Robot.HomeOrgin(AdrNo, txn.Seq);
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
                    case "ALIGNER":
                        switch (txn.Method)
                        {
                            case Transaction.Command.AlignerType.SetAlign:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().Aligner.SetSize(AdrNo, txn.Seq,txn.Value);
                                break;
                            case Transaction.Command.AlignerType.GetStatus:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().Aligner.Status(AdrNo, txn.Seq);
                                break;
                            case Transaction.Command.AlignerType.GetCombineStatus:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().Aligner.CombinedStatus(AdrNo, txn.Seq);
                                break;
                            case Transaction.Command.AlignerType.GetSpeed:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().Aligner.Speed(AdrNo, txn.Seq);
                                break;
                            case Transaction.Command.AlignerType.GetRIO:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().Aligner.StatusIO(AdrNo, txn.Seq, txn.Value);
                                break;
                            case Transaction.Command.AlignerType.Stop:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().Aligner.DeviceStop(AdrNo, txn.Seq, txn.Value);
                                break;
                            case Transaction.Command.AlignerType.Pause:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().Aligner.DevicePause(AdrNo, txn.Seq);
                                break;
                            case Transaction.Command.AlignerType.Continue:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().Aligner.DeviceContinue(AdrNo, txn.Seq);
                                break;
                            case Transaction.Command.AlignerType.GetSV:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().Aligner.SolenoidValve(AdrNo, txn.Seq, txn.Value);
                                break;
                            case Transaction.Command.AlignerType.GetError:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().Aligner.ErrorMessage(AdrNo, txn.Seq, txn.Value);
                                break;
                            case Transaction.Command.AlignerType.GetMode:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().Aligner.GetMode(AdrNo, txn.Seq);
                                break;
                            case Transaction.Command.AlignerType.AlignerHome:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().Aligner.Home(AdrNo, txn.Seq);
                                break;
                            case Transaction.Command.AlignerType.Align:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().Aligner.Align(AdrNo, txn.Seq, txn.Value);
                                break;
                            case Transaction.Command.AlignerType.AlignOption:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().Aligner.Align(AdrNo, txn.Seq, txn.Value, "1", "0", "0");
                                break;
                            case Transaction.Command.AlignerType.AlignOffset://使用上次Align結果，不用先回Home
                                txn.CommandEncodeStr = Ctrl.GetEncoder().Aligner.Align(AdrNo, txn.Seq, txn.Value, "0", "0", "0");
                                break;
                            case Transaction.Command.AlignerType.Retract:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().Aligner.Retract(AdrNo, txn.Seq);
                                break;
                            case Transaction.Command.AlignerType.WaferRelease:
                                //txn.CommandEncodeStr = Ctrl.GetEncoder().Aligner.WaferReleaseHold(AdrNo, txn.Seq);
                                txn.CommandEncodeStr = Ctrl.GetEncoder().Aligner.WaferReleaseHold(AdrNo, txn.Seq);
                                break;
                            case Transaction.Command.AlignerType.WaferHold:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().Aligner.WaferHold(AdrNo, txn.Seq);
                                break;
                            case Transaction.Command.AlignerType.AlignerServo:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().Aligner.ServoOn(AdrNo, txn.Seq, txn.Value);
                                break;
                            case Transaction.Command.AlignerType.AlignerMode:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().Aligner.Mode(AdrNo, txn.Seq, txn.Value);
                                break;
                            case Transaction.Command.AlignerType.AlignerOrigin:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().Aligner.OrginSearch(AdrNo, txn.Seq);
                                break;
                            case Transaction.Command.AlignerType.Reset:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().Aligner.ErrorReset(AdrNo, txn.Seq);
                                break;
                            case Transaction.Command.AlignerType.AlignerSpeed:
                                txn.CommandEncodeStr = Ctrl.GetEncoder().Aligner.setSpeed(AdrNo, txn.Seq, txn.Value);
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

                if (this.Type.Equals("ROBOT"))
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
                    this.InitialComplete = false;
                    if ((this.InterLock || !(this.UnLockByJob.Equals(TargetJob.Job_Id) || this.UnLockByJob.Equals(""))) && !Force)
                    {
                        ReturnMessage tmp = new ReturnMessage();
                        tmp.Value = "Interlock!";
                        logger.Error(this.Name + " Interlock! Txn:" + JsonConvert.SerializeObject(txn));
                        ControllerManagement.Get(Controller)._ReportTarget.On_Command_Error(this, txn, tmp);

                        return false;
                    }
                    if (this.Type.Equals("LOADPORT"))
                    {
                        this.InterLock = true;
                    }
                }
                if (txn.TargetJobs.Count == 0)
                {

                    Job dummy = new Job();
                    dummy.Job_Id = "dummy";
                    txn.TargetJobs.Add(dummy);

                }
                if (Ctrl.DoWork(txn))
                {

                    result = true;
                }
                else
                {
                    logger.Debug("SendCommand fail.");
                    result = false;
                    if (this.Type.Equals("LOADPORT"))
                    {
                        this.InterLock = false;
                    }
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
