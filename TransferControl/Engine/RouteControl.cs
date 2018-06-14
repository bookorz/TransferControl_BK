using log4net;
using Newtonsoft.Json;
using SANWA.Utility;
using TransferControl.Config;
using TransferControl.Controller;

using TransferControl.Management;


using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TransferControl.Parser;

namespace TransferControl.Engine
{
    public class RouteControl : AlarmMapping, ICommandReport
    {
        private static readonly ILog logger = LogManager.GetLogger(typeof(RouteControl));
        string _Mode = "";
        public bool IsInitial = false;
        DateTime StartTime = new DateTime();
        IEngineReport _EngReport;
        public static SystemConfig SysConfig;

        public int SpinWaitTimeOut = 99999000;

        public RouteControl(IEngineReport ReportTarget)
        {
            _EngReport = ReportTarget;
            ConfigTool<SystemConfig> SysCfg = new ConfigTool<SystemConfig>();
            SysConfig = SysCfg.ReadFile("config/SystemConfig.json");

            ConfigTool<DeviceConfig> DeviceCfg = new ConfigTool<DeviceConfig>();
            foreach (DeviceConfig eachDevice in DeviceCfg.ReadFileByList("config/Controller/Controllers.json"))
            {
                if (!eachDevice.Enable)
                {
                    continue;
                }
                DeviceController ctrl = new DeviceController(eachDevice, this);
                //ctrl.Connect();
                ControllerManagement.Add(eachDevice.DeviceName, ctrl);
            }
            ConfigTool<Node> NodeCfg = new ConfigTool<Node>();

            foreach (Node eachNode in NodeCfg.ReadFileByList("config/Node/Nodes.json"))
            {
                eachNode.InitialObject();
                NodeManagement.Add(eachNode.Name, eachNode);

            }

            PathManagement.LoadConfig();
            CommandScriptManagement.LoadConfig();
            CmdParamManagement.Initialize();
        }

        public void ConnectAll()
        {
            ControllerManagement.ConnectAll();
        }

        public void DisconnectAll()
        {
            ControllerManagement.DisonnectAll();
        }

        public void Continue()
        {
            if (_Mode.Equals("Pause"))
            {
                _Mode = "Start";
                foreach (Node node in NodeManagement.GetList())
                {
                    if (node.Type.Equals("Robot"))
                    {
                        Transaction txn = new Transaction();
                        txn.Method = Transaction.Command.RobotType.Continue;
                        node.SendCommand(txn);
                        node.State = "Run";
                    }
                    else if (node.Type.Equals("Aligner"))
                    {
                        Transaction txn = new Transaction();
                        txn.Method = Transaction.Command.AlignerType.Continue;
                        node.SendCommand(txn);
                        node.State = "Run";
                    }
                }
            }
            else
            {
                throw new Exception("狀態錯誤:無法執行");
            }
        }
        public void Pause()
        {
            if (_Mode.Equals("Start"))
            {
                _Mode = "Pause";
                foreach (Node node in NodeManagement.GetList())
                {
                    if (node.Type.Equals("Robot") || node.Type.Equals("Aligner"))
                    {
                        Transaction txn = new Transaction();
                        txn.Method = Transaction.Command.RobotType.Pause;
                        node.SendCommand(txn);
                        node.State = "Pause";
                    }
                }
            }
            else
            {
                throw new Exception("狀態錯誤:尚未啟動");
            }
        }

        public void Stop()
        {
            lock (this)
            {
                _Mode = "Stop";
                IsInitial = false;
            }

        }

        public void Start(object ScriptName)
        {
            lock (this)
            {
                if (_Mode == "Start")
                {
                    throw new Exception("目前已在Start模式");
                }
                //else if (NodeManagement.IsNeedInitial())
                //{
                //    throw new Exception("請先執行initial");
                //}
                else
                {
                    _Mode = "Start";
                    //檢查各狀態
                    foreach(Node port in NodeManagement.GetLoadPortList())
                    {
                        Transaction txn = new Transaction();
                        txn.Method = Transaction.Command.LoadPortType.ReadStatus;
                        txn.FormName = "StartMode";
                        port.SendCommand(txn);
                    }
                }
            }
            while (_Mode.Equals("Start"))
            {


                StartTime = DateTime.Now;
                while (true)
                {
                    foreach (Node each in NodeManagement.GetLoadPortList())
                    {
                        each.Available = false;
                    }

                    logger.Debug("等待可用Foup中");
                    SpinWait.SpinUntil(() => (from LD in NodeManagement.GetLoadPortList()
                                              where LD.Available == true && LD.Mode.Equals("LD")
                                              select LD).Count() != 0 || _Mode.Equals("Stop"), SpinWaitTimeOut);
                    if ((from LD in NodeManagement.GetLoadPortList()
                         where LD.Available == true
                         select LD).Count() != 0 || _Mode.Equals("Stop"))
                    {
                        if (!_Mode.Equals("Start"))
                        {
                            logger.Debug("結束Start模式");
                            return;
                        }
                        else
                        {
                            logger.Debug("可用Foup出現");
                        }
                        break;
                    }
                }
                foreach (Node robot in NodeManagement.GetEnableRobotList())
                {
                    robot.InitialObject();

                    List<Node> PortList = new List<Node>();
                    foreach (Node.Route eachNode in robot.RouteTable)
                    {
                        if (eachNode.NodeType.Equals("Port"))
                        {
                            PortList.Add(NodeManagement.Get(eachNode.NodeName));

                        }
                    }
                    var findPort = from port in PortList
                                   where port.Available == true && port.Fetchable == true
                                   select port;

                    if (findPort.Count() == 0)
                    {//當沒有Port正在作業時，指定Load的時間最早的Port可以開始取片

                        findPort = from port in PortList
                                   where port.Available
                                   select port;
                        if (findPort.Count() == 0)
                        {


                            if (!_Mode.Equals("Start"))
                            {
                                return;
                            }
                            findPort = from port in PortList
                                       where port.Available
                                       select port;
                        }
                        if (findPort.Count() != 0)
                        {
                            List<Node> tmp = findPort.ToList();

                            tmp.Sort((x, y) => { return x.LoadTime.CompareTo(y.LoadTime); });

                            tmp[0].Fetchable = true;
                            logger.Debug(robot.Name + ":指定 " + tmp[0].Name + " 開始取片");
                        }
                        else
                        {
                            logger.Debug("RobotFetchMode " + robot.Name + " 找不到可以搬的Port");
                            robot.Phase = "2";
                            robot.GetAvailable = true;//標記目前Robot可以接受其他搬送命令 
                            robot.Release = true;
                            continue;
                        }
                    }

                    RobotFetchMode(robot, ScriptName.ToString());
                }
                logger.Debug("等待搬運週期完成");
                SpinWait.SpinUntil(() => CheckCycle() || _Mode.Equals("Stop"), SpinWaitTimeOut); //等待搬運週期完成
                logger.Debug("搬運週期完成，下個周期開始");
            }
            logger.Debug("結束Start模式");
        }

        private bool CheckCycle()
        {


            bool a = (from rbt in NodeManagement.GetEnableRobotList()
                      where rbt.Phase.Equals("1")
                      select rbt).Count() == 0;
            bool b = (from jb in JobManagement.GetJobList()
                      where jb.Position.Equals("Robot01") || jb.Position.Equals("Robot02") || jb.Position.Equals("Aligner01") || jb.Position.Equals("Aligner02")
                      select jb).Count() == 0;

            return a && b;
        }

        private bool CheckPresent(Node Node)
        {
            bool a = (from jb in Node.JobList.Values
                      where jb.MapFlag == true
                      select jb).Count() != 0;

            return a;
        }

        public string GetMode()
        {
            string result = "";
            lock (this)
            {
                result = _Mode;
            }

            return result;
        }

        private void RobotFetchMode(Node RobotNode, string ScriptName)
        {
            RobotNode.Phase = "1";
            RobotNode.GetAvailable = false;
            RobotNode.PutAvailable = false;
            RobotNode.AllDone = false;
            RobotNode.Release = false;
            if (RobotNode.JobList.Count == 0)//雙臂皆空
            {

                RobotNode.GetAvailable = false;

                List<Job> TargetJobs = new List<Job>();
                List<Node> PortList = new List<Node>();
                foreach (Node.Route eachNode in RobotNode.RouteTable)
                {
                    if (eachNode.NodeType.Equals("Port"))
                    {
                        PortList.Add(NodeManagement.Get(eachNode.NodeName));

                    }
                }
                var findPort = from port in PortList
                               where port.Available == true && port.Fetchable == true
                               select port;

                if (findPort.Count() == 0)
                {
                    logger.Debug("RobotFetchMode " + RobotNode.Name + " 找不到可以搬的Port");
                    RobotNode.Phase = "2";
                    RobotNode.GetAvailable = true;//標記目前Robot可以接受其他搬送命令 
                    RobotNode.Release = true;
                    return;
                }
                foreach (Node PortNode in findPort)
                {

                    int FirstSlot = -1;
                    bool ConsecutiveSlot = false;

                    if (PortNode.Type.Equals("LoadPort"))
                    {//搜尋Port有沒有要處理的Wafer

                        if (!PortNode.Fetchable || !PortNode.Available)
                        {
                            continue;
                        }
                        List<Job> JobsSortBySlot = PortNode.JobList.Values.ToList();
                        var findJob = from Job in JobsSortBySlot
                                      where Job.ProcessFlag == false
                                      select Job;

                        if (findJob.Count() == 0)
                        {
                            PortNode.Fetchable = false;
                            PortNode.Available = false;
                            logger.Debug("RobotFetchMode " + RobotNode.Name + " 找不到可以搬的Wafer");
                            RobotNode.Phase = "2";
                            RobotNode.GetAvailable = true;//標記目前Robot可以接受其他搬送命令 
                            RobotNode.Release = true;
                            TimeSpan diff = DateTime.Now - StartTime;
                            logger.Info("Process Time: " + diff.TotalSeconds);
                            _EngReport.On_Node_State_Changed(PortNode, "Ready To UnLoad");
                            _EngReport.On_Port_Finished(PortNode.Name);

                        }
                        else
                        {
                            JobsSortBySlot = findJob.ToList();
                            JobsSortBySlot.Sort((x, y) => { return Convert.ToInt16(x.Slot).CompareTo(Convert.ToInt16(y.Slot)); });
                            foreach (Job eachJob in JobsSortBySlot)
                            {
                                if (!eachJob.ProcessFlag)
                                {
                                    if (FirstSlot == -1)
                                    {
                                        FirstSlot = Convert.ToInt16(eachJob.Slot);

                                        TargetJobs.Add(eachJob);
                                        RobotNode.CurrentLoadPort = PortNode.Name;
                                        //找到第一片
                                        eachJob.FetchRobot = RobotNode.Name;

                                        NodeManagement.GetReservAligner(eachJob.FromPort).UnLockByJob = eachJob.Job_Id;
                                    }
                                    else
                                    {
                                        int diff = Convert.ToInt16(eachJob.Slot) - FirstSlot;
                                        if (diff == 1)
                                        {
                                            ConsecutiveSlot = true;
                                            TargetJobs.Add(eachJob);

                                        }
                                        else
                                        {

                                            ConsecutiveSlot = false;
                                        }

                                        eachJob.FetchRobot = RobotNode.Name;
                                        break;//找到第二片
                                    }
                                }
                            }
                            if (FirstSlot != -1)
                            {
                                if (ConsecutiveSlot)//雙臂同取
                                {

                                    Transaction txn = new Transaction();
                                    txn.TargetJobs = TargetJobs;
                                    txn.Position = PortNode.Name;
                                    txn.Slot = (FirstSlot + 1).ToString();
                                    txn.Method = Transaction.Command.RobotType.DoubleGet;
                                    txn.Arm = "";
                                    txn.ScriptName = ScriptName;
                                    if (RobotNode.SendCommand(txn))
                                    {
                                        Node NextRobot = NodeManagement.GetNextRobot(TargetJobs[0].Destination);


                                        NextRobot.WaitForCarryCount += 2;
                                        //logger.Debug(NextRobot.Name + " WaitForCarryCount:" + NextRobot.Status.WaitForCarryCount);

                                    }

                                }
                                else//單臂輪取 R軸
                                {
                                    Transaction txn = new Transaction();
                                    txn.TargetJobs = TargetJobs;
                                    txn.Position = PortNode.Name;
                                    txn.Slot = FirstSlot.ToString();
                                    txn.Method = Transaction.Command.RobotType.Get;
                                    txn.Arm = "1";
                                    txn.ScriptName = ScriptName;
                                    if (RobotNode.SendCommand(txn))
                                    {
                                        Node NextRobot = NodeManagement.GetNextRobot(TargetJobs[0].Destination);

                                        NextRobot.WaitForCarryCount += 1;
                                        //logger.Debug(NextRobot.Name + " WaitForCarryCount:" + NextRobot.Status.WaitForCarryCount);

                                    }
                                }

                                break;
                            }
                        }
                    }
                }

            }
            else if (RobotNode.JobList.Count == 1)//單臂有片
            {
                Node PortNode = NodeManagement.Get(RobotNode.CurrentLoadPort);
                int FirstSlot = -1;

                //搜尋Port有沒有要處理的Wafer

                List<Job> TargetJobs = new List<Job>();
                List<Job> JobsSortBySlot = new List<Job>();
                if (PortNode.JobList.Count != 0)
                {
                    JobsSortBySlot = PortNode.JobList.Values.ToList();
                }
                var findJob = from Job in JobsSortBySlot
                              where Job.ProcessFlag == false
                              select Job;
                JobsSortBySlot = findJob.ToList();
                JobsSortBySlot.Sort((x, y) => { return Convert.ToInt16(x.Slot).CompareTo(Convert.ToInt16(y.Slot)); });
                foreach (Job eachJob in JobsSortBySlot)
                {
                    if (!eachJob.Destination.Equals(PortNode.Name))
                    {
                        if (FirstSlot == -1)
                        {
                            FirstSlot = Convert.ToInt16(eachJob.Slot);
                            TargetJobs.Add(eachJob);
                            break;//找到
                        }

                    }
                }
                if (FirstSlot != -1)
                {
                    //單臂輪取 L軸
                    Transaction txn = new Transaction();
                    txn.TargetJobs = TargetJobs;
                    txn.Position = PortNode.Name;
                    txn.Slot = FirstSlot.ToString();
                    txn.Method = Transaction.Command.RobotType.Get;
                    txn.Arm = "2";
                    txn.ScriptName = ScriptName;
                    if (RobotNode.SendCommand(txn))
                    {
                        Node NextRobot = NodeManagement.GetNextRobot(TargetJobs[0].Destination);

                        NextRobot.WaitForCarryCount += 1;
                        // logger.Debug(NextRobot.Name + " WaitForCarryCount:" + NextRobot.Status.WaitForCarryCount);

                    }
                }
                else
                {
                    //已沒有
                    RobotNode.Phase = "2";//進入處理階段
                    foreach (Job eachJob in RobotNode.JobList.Values.ToList())
                    {
                        eachJob.CurrentState = Job.State.WAIT_PUT;
                    }
                    FindNextJob(RobotNode, ScriptName);
                }


            }
            else if (RobotNode.JobList.Count == 2)//雙臂有片
            {
                RobotNode.Phase = "2";//進入處理階段

                FindNextJob(RobotNode, ScriptName);
            }

        }


        private void FindNextJob(Node RobotNode, string ScriptName)
        {

            var find = from job in RobotNode.JobList.Values.ToList()
                       where !job.ProcessFlag
                       select job;
            string lastProcessNode = "";
            if (find.Count() != 0)
            {
                RobotNode.PutAvailable = true;
                List<Job> Js = find.ToList();
                Js.Sort((x, y) => { return Convert.ToInt16(x.Slot).CompareTo(Convert.ToInt16(y.Slot)); });
                foreach (Job each in Js)
                {
                    logger.Debug("等待可以放片時機:" + each.Job_Id);
                    SpinWait.SpinUntil(() => RobotNode.PutAvailable || _Mode.Equals("Stop"), SpinWaitTimeOut); //等待可以放片時機
                    if (_Mode.Equals("Stop"))
                    {
                        logger.Debug("離開自動模式");
                        return;
                    }
                    logger.Debug("可以放片:" + each.Job_Id);
                    RobotNode.PutAvailable = false;
                    if (lastProcessNode.Equals(""))
                    {

                        each.ProcessNode = NodeManagement.GetReservAligner(each.FromPort).Name;
                        lastProcessNode = each.ProcessNode;
                    }
                    else
                    {
                        //each.ProcessNode = NodeManagement.GetAnotherAligner(lastProcessNode).Name;
                        each.ProcessNode = NodeManagement.GetReservAligner(each.FromPort).Name;
                    }
                    List<Job> TargetJobs = new List<Job>();
                    TargetJobs.Add(each);
                    try
                    {
                        foreach (Path eachPath in PathManagement.GetPath(ScriptName, "WAIT_PROCESS", ""))
                        {
                            each.CurrentState = eachPath.ChangeToStatus;
                            foreach (Path.Action eachAction in eachPath.TodoList)
                            {

                                TodoAction(ScriptName, eachAction, TargetJobs, RobotNode);
                            }
                            break;
                        }
                    }
                    catch (Exception ee)
                    {
                        string ttt = "";
                    }
                }
            }
            else
            {

                logger.Debug("沒有東西可以做");

            }
        }

        private Node GetPosNode(string Position, Job Job, Node Node)
        {
            Node result = null;
            switch (Position)
            {
                case "Job.Position":
                    result = NodeManagement.Get(Job.Position);
                    break;
                case "ReserveAligner":
                    result = NodeManagement.GetReservAligner(Job.FromPort);
                    break;
                case "Aligner":
                    logger.Debug("Node.CurrentPosition:" + Node.CurrentPosition);
                    //result = NodeManagement.GetAligner(Node.CurrentPosition, Job.FromPort);
                    result = NodeManagement.Get(Job.ProcessNode);
                    break;
                case "OCR":

                    break;
                case "Robot":
                    result = NodeManagement.Get(Job.Position);
                    if (!result.Type.Equals("Robot"))
                    {
                        result = NodeManagement.Get(Job.LastNode);
                    }
                    if (!result.Type.Equals("Robot"))
                    {
                        logger.Debug("(GetPosNode) Robot not found");
                        return null;
                    }
                    break;
                case "NextRobot":
                    Node Target = NodeManagement.GetReservAligner(Job.FromPort);
                    if (Target == null)
                    {
                        logger.Debug("(GetPosNode) Target not found.");
                        return null;
                    }
                    result = NodeManagement.GetNextRobot(Target, Job);
                    if (result == null)
                    {
                        logger.Debug("(GetPosNode) GetNextRobot fail.");
                        return null;
                    }
                    break;
            }
            return result;
        }

        private void GetArmSlot(ref Transaction txn, Node Node, Job TargetJob)
        {
            Node target = NodeManagement.Get(txn.Position);
            if (target != null)
            {
                switch (txn.Method)
                {
                    case Transaction.Command.RobotType.Put:
                    case Transaction.Command.RobotType.PutWithoutBack:
                        if (target.Type.Equals("LoadPort"))
                        {
                            txn.Slot = TargetJob.DestinationSlot;
                            txn.Arm = TargetJob.Slot;
                        }
                        else
                        {
                            txn.Slot = "1";
                            txn.Arm = TargetJob.Slot;
                        }
                        break;
                    case Transaction.Command.RobotType.PutBack:
                        txn.Slot = "1";
                        txn.Arm = Node.PutOutArm;
                        break;
                    case Transaction.Command.RobotType.GetWait:
                        txn.Arm = "1";
                        txn.Slot = "1";
                        break;
                    case Transaction.Command.RobotType.WaitBeforeGet:
                    case Transaction.Command.RobotType.Get:
                    case Transaction.Command.RobotType.GetAfterWait:
                        txn.Slot = TargetJob.Slot;
                        if (!Node.JobList.ContainsKey("1"))
                        {
                            txn.Arm = "1";
                        }
                        else if (!Node.JobList.ContainsKey("2"))
                        {
                            txn.Arm = "2";
                        }
                        else
                        {
                            logger.Debug("AlignerAction State.WAIT_WRLS:兩隻手臂都有東西，無法再拿:" + Node.Name);
                        }
                        break;
                }
            }
            else
            {
                logger.Debug("txn.Position:" + txn.Position + " is not found.");
            }
        }

        private void TodoAction(string ScriptName, Path.Action Action, List<Job> TargetJobs, Node FinNode)
        {
            try
            {

                bool Force = false;
                Node Node = null; ;
                if (TargetJobs.Count == 0)
                {
                    logger.Debug("TodoAction TargetJobs is empty.");
                    logger.Debug(JsonConvert.SerializeObject(Action));
                    return;
                }
                Job TargetJob = TargetJobs[0];


                Transaction txn = new Transaction();
                txn.ScriptName = ScriptName;
                Node Target = null;
                if (!Action.Position.Equals(""))
                {
                    Target = GetPosNode(Action.Position, TargetJob, FinNode);
                    if (Target == null)
                    {
                        logger.Debug("TodoAction Target is null.");
                        logger.Debug(JsonConvert.SerializeObject(Action));
                        return;
                    }
                    txn.Position = Target.Name;
                }
                else
                {
                    txn.Position = "";
                }
                txn.TargetJobs = TargetJobs;

                txn.Method = Action.Method;
                switch (Action.EqpType)
                {
                    case "Robot":
                        Node = GetPosNode(Action.EqpType, TargetJob, FinNode);
                        if (!Node.Type.Equals("Robot"))
                        {
                            Node = NodeManagement.Get(TargetJob.LastNode);
                        }
                        if (!Node.Type.Equals("Robot"))
                        {
                            logger.Debug("(TodoAction) Robot not found");
                            logger.Debug(JsonConvert.SerializeObject(Action));
                            return;
                        }
                        while (true)//等待主控權
                        {


                            if (Node.PutOut && !Action.Method.Equals(Transaction.Command.RobotType.GetAfterWait) && !Action.Method.Equals(Transaction.Command.RobotType.PutBack))
                            {
                                logger.Debug(Node.Name + " 偵測到手臂伸出中，等待收回，需求命令:" + Action.EqpType + ":" + Action.Method);

                                SpinWait.SpinUntil(() => !Node.PutOut || _Mode.Equals("Stop"), SpinWaitTimeOut); //等待Robot手臂收回
                                if (_Mode.Equals("Stop"))
                                {
                                    logger.Debug("離開自動模式");
                                    return;
                                }
                                logger.Debug(Node.Name + " 偵測到手臂收回，離開等待，需求命令:" + Action.EqpType + ":" + Action.Method);
                            }
                            if (CheckPresent(Target) && (Action.Method.Equals(Transaction.Command.RobotType.Put) || Action.Method.Equals(Transaction.Command.RobotType.PutWithoutBack)))
                            {
                                logger.Debug(Node.Name + " 偵測到目標在席存在中，等待，需求命令:" + Action.EqpType + ":" + Action.Method);
                                SpinWait.SpinUntil(() => !CheckPresent(Target) || _Mode.Equals("Stop"), SpinWaitTimeOut);
                                if (_Mode.Equals("Stop"))
                                {
                                    logger.Debug("離開自動模式");
                                    return;
                                }
                                logger.Debug(Node.Name + " 偵測到目標在席已被取走，離開等待，需求命令:" + Action.EqpType + ":" + Action.Method);
                            }

                            logger.Debug(Node.Name + " 等待主控權 " + Action.EqpType + ":" + Action.Method + ":" + TargetJob.Job_Id);
                            logger.Debug(JsonConvert.SerializeObject(Node));
                            SpinWait.SpinUntil(() => (!Node.InterLock && (Node.UnLockByJob.Equals(TargetJob.Job_Id) || Node.UnLockByJob.Equals(""))) || Force || _Mode.Equals("Stop"), SpinWaitTimeOut); //等待Robot有空
                            if (_Mode.Equals("Stop"))
                            {
                                logger.Debug("離開自動模式");
                                return;
                            }
                            lock (Node)
                            {

                                if (CheckPresent(Target) && (Action.Method.Equals(Transaction.Command.RobotType.Put) || Action.Method.Equals(Transaction.Command.RobotType.PutWithoutBack)))
                                {
                                    logger.Debug(Node.Name + " 偵測到目標在席存在中，等待，需求命令:" + Action.EqpType + ":" + Action.Method);
                                    continue;
                                }
                                if (Node.PutOut && !Action.Method.Equals(Transaction.Command.RobotType.GetAfterWait) && !Action.Method.Equals(Transaction.Command.RobotType.PutBack))
                                {
                                    logger.Debug(Node.Name + " 偵測到手臂伸出中，等待收回，需求命令:" + Action.EqpType + ":" + Action.Method);
                                    continue;
                                }
                                Node.GetAvailable = false;//鎖定Robot
                                if (Action.Method.Equals(Transaction.Command.RobotType.WaitBeforeGet) || Action.Method.Equals(Transaction.Command.RobotType.PutWithoutBack))
                                {
                                    Node.UnLockByJob = TargetJob.Job_Id;
                                }

                                logger.Debug(Node.Name + " 取得主控權，離開排隊:" + Action.EqpType + ":" + Action.Method);
                                break;//取得主控權，離開排隊
                                      //}
                            }
                        }


                        GetArmSlot(ref txn, Node, TargetJob);

                        Node.SendCommand(txn);


                        break;
                    case "NextRobot":

                        Node = GetPosNode(Action.EqpType, TargetJob, FinNode);


                        if (Node == null)
                        {
                            logger.Debug("TodoAction GetNextRobot fail.");
                            return;
                        }


                        TargetJob.WaitToDo = Action.Method;


                        while (true)//第二段 等待主控權
                        {
                            logger.Debug(Node.Name + " 等待Robot主控權 :" + Action.EqpType + ":" + Action.Method);
                            //logger.Debug(JsonConvert.SerializeObject(Node));
                            SpinWait.SpinUntil(() => Node.Phase.Equals("2") && ((Node.GetAvailable && Node.GetMutex && Node.JobList.Count < 2) || (Node.GetAvailable && !Node.InterLock && (Node.UnLockByJob.Equals(TargetJob.Job_Id) || Node.UnLockByJob.Equals("")) && Node.JobList.Count < 2) || Force || !TargetJob.WaitToDo.Equals(Action.Method)) || _Mode.Equals("Stop"), SpinWaitTimeOut); //等待Robot有空
                            if (_Mode.Equals("Stop"))
                            {
                                logger.Debug("離開自動模式");
                                return;
                            }
                            logger.Debug(JsonConvert.SerializeObject(Node));
                            logger.Debug("TargetJob.Job_Id:" + TargetJob.Job_Id);
                            if (Node.PutOut && !Action.Method.Equals(Transaction.Command.RobotType.GetAfterWait) && !Action.Method.Equals(Transaction.Command.RobotType.PutBack) && !Action.Method.Equals(Transaction.Command.RobotType.Get))
                            {
                                logger.Debug(Node.Name + " 偵測到手臂伸出中，等待收回，需求命令:" + Action.EqpType + ":" + Action.Method);

                                SpinWait.SpinUntil(() => !Node.PutOut || _Mode.Equals("Stop"), SpinWaitTimeOut); //等待Robot手臂收回
                                if (_Mode.Equals("Stop"))
                                {
                                    logger.Debug("離開自動模式");
                                    return;
                                }
                                logger.Debug(Node.Name + " 偵測到手臂收回，離開等待，需求命令:" + Action.EqpType + ":" + Action.Method);
                            }

                            lock (Node)
                            {
                                if (Node.PutOut && !Action.Method.Equals(Transaction.Command.RobotType.GetAfterWait) && !Action.Method.Equals(Transaction.Command.RobotType.PutBack) && !Action.Method.Equals(Transaction.Command.RobotType.Get))
                                {
                                    logger.Debug(Node.Name + " 偵測到手臂伸出中:" + Action.EqpType + ":" + Action.Method);
                                    continue;
                                }
                                if (!TargetJob.WaitToDo.Equals(Action.Method))
                                {
                                    logger.Debug(Node.Name + " 偵測 " + Action.EqpType + " 需求命令 " + Action.Method + " 已改變為 :" + TargetJob.WaitToDo + "，結束等待");
                                    return;
                                }
                                else if (Node.Phase.Equals("2") && ((Node.GetAvailable && Node.GetMutex && Node.JobList.Count < 2) || (Node.GetAvailable && !Node.InterLock && (Node.UnLockByJob.Equals(TargetJob.Job_Id) || Node.UnLockByJob.Equals("")) && Node.JobList.Count < 2) || Force))
                                {

                                    logger.Debug(JsonConvert.SerializeObject(Node));
                                    Node.Reserve = false;//釋放預約
                                    Node.GetMutex = false;//鎖定Robot

                                    Node.InterLock = true;
                                    if (Action.Method.Equals(Transaction.Command.RobotType.WaitBeforeGet) || Action.Method.Equals(Transaction.Command.RobotType.PutWithoutBack))
                                    {
                                        Node.UnLockByJob = TargetJob.Job_Id;
                                    }
                                    //Node.PutAvailable = false;
                                    logger.Debug(Node.Name + " 取得主控權，離開排隊 :" + Action.EqpType + ":" + Action.Method);

                                    break;//取得主控權，離開排隊
                                }
                                else
                                {
                                    logger.Debug(Node.Name + " 主控權被搶先，重新排隊 :" + Action.EqpType + ":" + Action.Method);
                                    //continue;//主控權被搶先，重新排隊
                                }
                            }
                        }


                        lock (TargetJob)
                        {
                            if (TargetJob.WaitToDo == "")
                            {
                                logger.Debug("已經做完，不需再做重複動作");
                                Node.GetMutex = true;
                                Node.InterLock = false;
                                return;//已經做完，不需再做重複動作
                            }
                            else
                            {
                                txn.Method = TargetJob.WaitToDo;
                                TargetJob.WaitToDo = "";//讓其他等待者不做重複動作

                                if (txn.Method.Equals(Transaction.Command.RobotType.Get) || txn.Method.Equals(Transaction.Command.RobotType.GetAfterWait))
                                {//當手臂伸出等待時，下一個動作只能是Option 3，沒伸出時可直接Option 0
                                    if (Node.PutOut)
                                    {
                                        txn.Method = Transaction.Command.RobotType.GetAfterWait;
                                    }
                                    else
                                    {
                                        txn.Method = Transaction.Command.RobotType.Get;
                                    }
                                }
                            }
                        }

                        GetArmSlot(ref txn, Node, TargetJob);

                        Node.SendCommand(txn);

                        break;
                    case "Aligner":
                    case "ReserveAligner":
                        Node = GetPosNode(Action.EqpType, TargetJob, FinNode);
                        while (true)//等待主控權
                        {
                            logger.Debug(Node.Name + " 等待主控權 " + Action.EqpType + ":" + Action.Method);
                            logger.Debug(Node.Name + " 等待主控權 " + Node.InterLock + "," + Node.UnLockByJob + "," + TargetJob.Job_Id);
                            SpinWait.SpinUntil(() => (!Node.InterLock && (Node.UnLockByJob.Equals("") || Node.UnLockByJob.Equals(TargetJob.Job_Id))) || Force || _Mode.Equals("Stop"), SpinWaitTimeOut);
                            if (_Mode.Equals("Stop"))
                            {
                                logger.Debug("離開自動模式");
                                return;
                            }
                            if (Action.Method.Equals(Transaction.Command.AlignerType.WaferHold))
                            {
                                logger.Debug(Node.Name + " 偵測到目標節點未就緒，等待，需求命令:" + Action.EqpType + ":" + Action.Method);

                                SpinWait.SpinUntil(() => Node.Available && Node.JobList.Count == 0 || _Mode.Equals("Stop"), SpinWaitTimeOut);
                                if (_Mode.Equals("Stop"))
                                {
                                    logger.Debug("離開自動模式");
                                    return;
                                }
                                Node.Available = false;
                                logger.Debug(Node.Name + " 偵測到目標節點就緒，離開等待，需求命令:" + Action.EqpType + ":" + Action.Method);
                            }

                            lock (Node)
                            {
                                if ((!Node.InterLock && (Node.UnLockByJob.Equals("") || Node.UnLockByJob.Equals(TargetJob.Job_Id))) || Force)
                                {
                                    Node.InterLock = true;
                                    logger.Debug(Node.Name + " 取得主控權，離開排隊:" + Action.EqpType + ":" + Action.Method);
                                    break;//取得主控權，離開排隊
                                }
                                else
                                {

                                    logger.Debug(Node.Name + " 主控權被搶先，重新排隊:" + Action.EqpType + ":" + Action.Method);
                                    //continue;//主控權被搶先，重新排隊

                                }
                            }
                        }

                        txn.Value = Action.Param;
                        Node.SendCommand(txn);
                        break;
                    case "OCR":
                        Node = GetPosNode(Action.EqpType, TargetJob, FinNode);
                        txn.Value = Action.Param;
                        Node.SendCommand(txn);
                        break;
                }
            }

            catch (Exception e)
            {
                logger.Error("(TodoAction)" + e.Message + "\n" + e.StackTrace);
            }

        }

        private void RobotPutMode(Node RobotNode, string ScriptName)
        {
            Job Wafer;
            List<Job> TargetJobs = new List<Job>();
            if (RobotNode.JobList.Count == 0)//雙臂皆空
            {

                RobotNode.Phase = "1";//進入取片階段
                RobotFetchMode(RobotNode, ScriptName);
            }
            else if (RobotNode.JobList.Count == 1)//單臂有片
            {
                //單臂放片
                Wafer = RobotNode.JobList.Values.ToList()[0];
                TargetJobs.Add(Wafer);

                Transaction txn = new Transaction();
                txn.TargetJobs = TargetJobs;
                txn.Position = Wafer.Destination;
                txn.Slot = Wafer.DestinationSlot;
                txn.Method = Transaction.Command.RobotType.Put;
                txn.Arm = Wafer.Slot;
                txn.ScriptName = ScriptName;
                RobotNode.SendCommand(txn);
            }
            else if (RobotNode.JobList.Count == 2)//雙臂有片
            {
                List<Job> Jobs = RobotNode.JobList.Values.ToList();
                Jobs.Sort((x, y) => { return Convert.ToInt16(x.DestinationSlot).CompareTo(Convert.ToInt16(y.DestinationSlot)); });
                if (Jobs[0].Destination.Equals(Jobs[1].Destination))
                {
                    int SlotDiff = Convert.ToInt16(Jobs[1].DestinationSlot) - Convert.ToInt16(Jobs[0].DestinationSlot);
                    if (SlotDiff == 1)
                    {//雙臂同放
                        Wafer = Jobs[1];
                        Transaction txn = new Transaction();
                        txn.TargetJobs = Jobs;
                        txn.Position = Wafer.Destination;
                        txn.Slot = (Convert.ToInt16(Wafer.DestinationSlot)).ToString();
                        txn.Method = Transaction.Command.RobotType.DoublePut;
                        txn.Arm = "";
                        txn.ScriptName = ScriptName;
                        RobotNode.SendCommand(txn);
                    }
                    else
                    {//單臂輪放
                        Wafer = RobotNode.JobList.Values.ToList()[0];
                        TargetJobs.Add(Wafer);

                        Transaction txn = new Transaction();
                        txn.TargetJobs = TargetJobs;
                        txn.Position = Wafer.Destination;
                        txn.Slot = Wafer.DestinationSlot;
                        txn.Method = Transaction.Command.RobotType.Put;
                        txn.Arm = Wafer.Slot;
                        txn.ScriptName = ScriptName;
                        RobotNode.SendCommand(txn);
                    }
                }


            }
        }




        public void On_Command_Excuted(Node Node, Transaction Txn, ReturnMessage Msg)
        {
            try
            {
                logger.Debug("On_Command_Excuted");
                Job TargetJob = null;
                if (Txn.TargetJobs.Count != 0)
                {
                    TargetJob = Txn.TargetJobs[0];

                    if (TargetJob.Job_Id.Equals("dummy") && !Txn.ScriptName.Equals(""))
                    {
                        if (Txn.LastOneScript)
                        {
                            if (!Txn.CommandType.Equals("CMD") && !Txn.CommandType.Equals("MOV"))
                            {
                                _EngReport.On_Script_Finished(Node, Txn.ScriptName, Txn.FormName);
                            }
                        }
                        else
                        {
                            foreach (CommandScript cmd in CommandScriptManagement.GetExcuteNext(Txn.ScriptName, Txn.Method))
                            {
                                if (Convert.ToInt16(cmd.Index) - Convert.ToInt16(Txn.ScriptIndex) != 1)
                                {
                                    continue;
                                }
                                Transaction txn = new Transaction();
                                txn.Method = cmd.Method;
                                txn.FormName = Txn.FormName;
                                txn.Arm = cmd.Arm;
                                txn.Position = cmd.Position;
                                txn.Slot = cmd.Slot;
                                txn.Value = cmd.Value;
                                txn.ScriptName = Txn.ScriptName;
                                txn.ScriptIndex = cmd.Index;
                                txn.TargetJobs = Txn.TargetJobs;
                                logger.Debug("Excute Script:" + Txn.ScriptName + " Method:" + txn.Method);
                                if (cmd.Flag.Equals("End"))
                                {
                                    txn.LastOneScript = true;
                                }
                                Node.SendCommand(txn);
                            }
                        }
                    }
                }
                if (Node.Type.Equals("Robot"))
                {
                    Node.CurrentPosition = Txn.Position;
                }
                if (_Mode.Equals("Start"))
                {
                    switch (Node.Phase)
                    {
                        case "1":

                            break;
                        case "2":


                            foreach (Path eachPath in PathManagement.GetPath(Txn.ScriptName, Txn.Method))
                            {
                                if (!eachPath.ChangeToStatus.Equals(""))
                                {
                                    TargetJob.CurrentState = eachPath.ChangeToStatus;
                                }
                                foreach (Path.Action eachAction in eachPath.TodoList)
                                {
                                    TodoAction(Txn.ScriptName, eachAction, Txn.TargetJobs, Node);
                                }
                                break;
                            }

                            break;
                        case "3":

                            break;
                    }
                    if (Txn.FormName.Equals("StartMode") && Txn.Method.Equals(Transaction.Command.LoadPortType.ReadStatus))
                    {
                        MessageParser parser = new MessageParser(Node.Brand);
                        Dictionary<string, string> content = parser.ParseMessage(Txn.Method, Msg.Value);
                        bool CheckResult = true;
                        foreach(KeyValuePair<string,string> each in content)
                        {
                            switch (each.Key)
                            {
                                case "FOUP Clamp Status":
                                    if (!each.Value.Equals("Open"))
                                    {
                                        CheckResult = false;
                                    }
                                    break;
                                case "Latch Key Status":
                                    if (!each.Value.Equals("Close"))
                                    {
                                        CheckResult = false;
                                    }
                                    break;
                                case "Cassette Presence":
                                    if (!each.Value.Equals("Normal position"))
                                    {
                                        CheckResult = false;
                                    }
                                    break;
                                case "Door Position":
                                    if (!each.Value.Equals("Close position"))
                                    {
                                        CheckResult = false;
                                    }
                                    break;
                            }
                        }
                        if (CheckResult)
                        {
                            Node.ExcuteScript("LoadPortFoupIn", "LoadPortFoup", true);
                        }
                        else
                        {
                            Node.ExcuteScript("LoadPortFoupOut", "LoadPortFoup", true);
                        }
                    }
                }
                _EngReport.On_Command_Excuted(Node, Txn, Msg);

            }
            catch (Exception e)
            {
                logger.Error(Node.Controller + "-" + Node.AdrNo + "(On_Command_Excuted)" + e.Message + "\n" + e.StackTrace);
            }
        }

        public void On_Command_Finished(Node Node, Transaction Txn, ReturnMessage Msg)
        {


            var watch = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                Job TargetJob = null;
                if (Txn.TargetJobs.Count != 0)
                {
                    TargetJob = Txn.TargetJobs[0];
                    logger.Debug("On_Command_Finished:" + Txn.Method + ":" + Txn.Method);
                    Node.InterLock = false;
                    if (TargetJob.Job_Id.Equals("dummy") && !Txn.ScriptName.Equals(""))
                    {
                        if (Txn.LastOneScript)
                        {

                            _EngReport.On_Script_Finished(Node, Txn.ScriptName, Txn.FormName);

                        }
                        else
                        {
                            foreach (CommandScript cmd in CommandScriptManagement.GetFinishNext(Txn.ScriptName, Txn.Method))
                            {
                                if (Convert.ToInt16(cmd.Index) - Convert.ToInt16(Txn.ScriptIndex) != 1)
                                {
                                    continue;
                                }
                                Transaction txn = new Transaction();
                                txn.Method = cmd.Method;
                                txn.FormName = Txn.FormName;
                                txn.Arm = cmd.Arm;
                                txn.Position = cmd.Position;
                                txn.Slot = cmd.Slot;
                                txn.Value = cmd.Value;
                                txn.ScriptName = Txn.ScriptName;
                                txn.ScriptIndex = cmd.Index;
                                txn.TargetJobs = Txn.TargetJobs;
                                if (cmd.Flag.Equals("End"))
                                {
                                    txn.LastOneScript = true;
                                }
                                logger.Debug("Excute Script:" + Txn.ScriptName + " Method:" + txn.Method);
                                Node.SendCommand(txn);
                            }
                        }
                    }
                }
                switch (Node.Type)
                {
                    case "Robot":
                        UpdateJobLocation(Node, Txn);
                        UpdateNodeStatus(Node, Txn);
                        if (_Mode.Equals("Start"))
                        {
                            switch (Node.Phase)
                            {
                                case "1":
                                    RobotFetchMode(Node, Txn.ScriptName);
                                    break;
                                case "2":
                                    //TargetJob = Txn.TargetJobs[0];

                                    foreach (Path eachPath in PathManagement.GetPath(Txn.ScriptName, TargetJob.CurrentState, Txn.Method))
                                    {
                                        if (!eachPath.ChangeToStatus.Equals(""))
                                        {
                                            TargetJob.CurrentState = eachPath.ChangeToStatus;
                                        }
                                        foreach (Path.Action eachAction in eachPath.TodoList)
                                        {
                                            TodoAction(Txn.ScriptName, eachAction, Txn.TargetJobs, Node);
                                        }
                                        break;
                                    }

                                    if ((Txn.Method.Equals(Transaction.Command.RobotType.Get) || Txn.Method.Equals(Transaction.Command.RobotType.GetAfterWait) || Txn.Method.Equals(Transaction.Command.RobotType.Put) || Txn.Method.Equals(Transaction.Command.RobotType.PutBack)) && ((Node.AllDone && Node.JobList.Count == 2) || (Node.AllDone && Node.WaitForCarryCount == 0)) && Node.Phase == "2" && Node.JobList.Count != 0)
                                    {//拿好拿滿就去放片吧
                                        logger.Debug("拿好拿滿就去放片吧");
                                        Node.Phase = "3";
                                        RobotPutMode(Node, Txn.ScriptName);
                                    }
                                    break;
                                case "3":
                                    RobotPutMode(Node, Txn.ScriptName);
                                    break;
                            }
                        }

                        break;
                    case "Aligner":

                        UpdateNodeStatus(Node, Txn);
                        if (_Mode.Equals("Start"))
                        {
                            //TargetJob = Txn.TargetJobs[0];
                            foreach (Path eachPath in PathManagement.GetPath(Txn.ScriptName, TargetJob.CurrentState, Txn.Method))
                            {
                                if (!eachPath.Expression.Equals(""))
                                {
                                    switch (eachPath.Expression)
                                    {
                                        case "[Job.AlignerFlag] == true":
                                            if (!TargetJob.AlignerFlag)
                                            {
                                                continue;
                                            }
                                            break;
                                        case "[Job.AlignerFlag] == false":
                                            if (TargetJob.AlignerFlag)
                                            {
                                                continue;
                                            }
                                            break;
                                        case "[Job.OCRFlag] == true":
                                            if (!TargetJob.OCRFlag)
                                            {
                                                continue;
                                            }
                                            break;
                                        case "[Job.OCRFlag] == false":
                                            if (TargetJob.OCRFlag)
                                            {
                                                continue;
                                            }
                                            break;
                                        case "[Job.AlignerFinished] == true":
                                            if (!TargetJob.AlignerFinished)
                                            {
                                                continue;
                                            }
                                            break;
                                        case "[Job.AlignerFinished] == false":
                                            if (TargetJob.AlignerFinished)
                                            {
                                                continue;
                                            }
                                            break;
                                    }
                                }

                                if (!eachPath.ChangeToStatus.Equals(""))
                                {
                                    TargetJob.CurrentState = eachPath.ChangeToStatus;
                                }
                                foreach (Path.Action eachAction in eachPath.TodoList)
                                {
                                    TodoAction(Txn.ScriptName, eachAction, Txn.TargetJobs, Node);
                                }
                                break;
                            }
                        }
                        break;
                    case "LoadPort":
                        UpdateNodeStatus(Node, Txn);
                        break;
                }
            }
            catch (Exception e)
            {
                logger.Error(Node.Controller + "-" + Node.AdrNo + "(On_Command_Finished)" + e.Message + "\n" + e.StackTrace);
            }
            watch.Stop();
            var elapsedMs = watch.ElapsedMilliseconds;
            logger.Debug("On_Command_Finished ProcessTime:" + elapsedMs.ToString());


            _EngReport.On_Command_Finished(Node, Txn, Msg);

        }

        private void UpdateNodeStatus(Node Node, Transaction Txn)
        {

            switch (Node.Type)
            {
                case "Robot":

                    switch (Txn.Method)
                    {
                        case Transaction.Command.RobotType.Get:
                        case Transaction.Command.RobotType.GetAfterWait:


                            if (Node.Phase.Equals("2"))
                            {
                                var find = from job in Node.JobList.Values.ToList()
                                           where !job.ProcessFlag
                                           select job;
                                if (find.Count() == 0)
                                {
                                    Node.AllDone = true;//手上的都做完了
                                }
                                else
                                {
                                    Node.AllDone = false;
                                }

                                Node NextRobot = NodeManagement.GetNextRobot(Txn.TargetJobs[0].Destination);

                                if (NextRobot != null)
                                {
                                    if (Txn.TargetJobs[0].ProcessFlag)
                                    {
                                        //扣掉待搬送數量
                                        NextRobot.WaitForCarryCount--;
                                        //logger.Debug(NextRobot.Name + " WaitForCarryCount:" + NextRobot.Status.WaitForCarryCount);

                                    }
                                }
                                else
                                {
                                    logger.Error(Txn.TargetJobs[0].Job_Id + "找不到目的地搬送Robot");
                                }

                                Node.PutOut = false;
                                Node.GetMutex = true;
                                Node.UnLockByJob = "";
                            }

                            break;
                        case Transaction.Command.RobotType.Put:
                        case Transaction.Command.RobotType.PutBack:
                            if (Node.Phase.Equals("2"))
                            {
                                var find = from job in Node.JobList.Values.ToList()
                                           where !job.ProcessFlag
                                           select job;
                                if (find.Count() == 0)
                                {
                                    Node.AllDone = true;//手上的都做完了
                                }
                                else
                                {
                                    Node.AllDone = false;
                                }

                                Node.GetAvailable = true;
                                Node.GetMutex = true;
                                Node.UnLockByJob = "";
                                Node.PutAvailable = true;
                                Node.PutOut = false;
                                if (Node.JobList.Count == 0 && Node.WaitForCarryCount == 0)
                                {
                                    logger.Debug("手臂已空，也無待搬送，進入取片狀態");
                                    Node.Phase = "1";
                                    RobotFetchMode(Node, Txn.ScriptName);
                                }

                            }

                            break;
                        case Transaction.Command.RobotType.WaitBeforeGet:
                        case Transaction.Command.RobotType.PutWithoutBack:
                            Node.PutOutArm = Txn.Arm;
                            Node.PutOut = true;
                            break;
                    }
                    break;
                case "Aligner":

                    switch (Txn.Method)
                    {
                        case Transaction.Command.AlignerType.WaferHold:

                            break;
                        case Transaction.Command.AlignerType.Retract:
                            Node.Available = true;
                            Node.UnLockByJob = "";

                            Node.GetAvailable = false;
                            Node.GetMutex = false;

                            // Node.PutAvailable = true;
                            break;
                    }
                    break;
                case "LoadPort":
                    switch (Txn.Method)
                    {
                        case Transaction.Command.LoadPortType.MappingLoad:
                            Node.IsMapping = true;
                            Node.InterLock = false;

                            break;
                        case Transaction.Command.LoadPortType.Unload:
                            _EngReport.On_Node_State_Changed(Node, "Transfer Ready");
                            break;
                        default:
                            Node.InterLock = true;
                            break;
                    }
                    break;
            }
            //logger.Debug(JsonConvert.SerializeObject(Node));

        }

        private void UpdateJobLocation(Node Node, Transaction Txn)
        {
            if (Txn.TargetJobs != null)
            {
                if (Txn.TargetJobs.Count != 0)
                {
                    if (Txn.TargetJobs[0].Job_Id.Equals("dummy"))
                    {
                        return;
                    }
                }
            }
            switch (Node.Type)
            {
                case "Robot":
                    switch (Txn.Method)
                    {
                        case Transaction.Command.RobotType.DoubleGet:
                            for (int i = 0; i < Txn.TargetJobs.Count; i++)
                            {
                                Node TargetNode5 = NodeManagement.Get(Txn.TargetJobs[i].Position);
                                Job tmp;
                                TargetNode5.JobList.TryRemove(Txn.TargetJobs[i].Slot, out tmp);
                                tmp = new Job();
                                tmp.Job_Id = "No wafer";
                                tmp.Slot = Txn.TargetJobs[i].Slot;
                                TargetNode5.JobList.TryAdd(Txn.TargetJobs[i].Slot, tmp);
                                Txn.TargetJobs[i].LastNode = Txn.TargetJobs[i].Position;
                                Txn.TargetJobs[i].Slot = (i + 1).ToString();
                                Txn.TargetJobs[i].Position = Node.Name;
                                Node.JobList.TryAdd(Txn.TargetJobs[i].Slot, Txn.TargetJobs[i]);
                                _EngReport.On_Job_Location_Changed(Txn.TargetJobs[i]);
                            }

                            break;
                        case Transaction.Command.RobotType.DoublePut:
                            for (int i = 0; i < Txn.TargetJobs.Count; i++)
                            {
                                Node TargetNode6 = NodeManagement.Get(Txn.Position);
                                Job tmp;
                                Node.JobList.TryRemove(Txn.TargetJobs[i].Slot, out tmp);
                                Txn.TargetJobs[i].LastNode = Txn.TargetJobs[i].Position;
                                switch (i)
                                {
                                    case 0:
                                        Txn.TargetJobs[i].Slot = (Convert.ToInt16(Txn.Slot) - 1).ToString();

                                        break;
                                    case 1:
                                        Txn.TargetJobs[i].Slot = Txn.Slot;

                                        break;
                                }


                                Txn.TargetJobs[i].Position = Txn.Position;
                                TargetNode6.JobList.TryRemove(Txn.TargetJobs[i].Slot, out tmp);
                                TargetNode6.JobList.TryAdd(Txn.TargetJobs[i].Slot, Txn.TargetJobs[i]);
                                _EngReport.On_Job_Location_Changed(Txn.TargetJobs[i]);
                            }

                            break;
                        case Transaction.Command.RobotType.Get://更新Wafer位置
                        case Transaction.Command.RobotType.GetAfterWait:

                            //logger.Debug(Txn.TargetJobs.Count.ToString());
                            for (int i = 0; i < Txn.TargetJobs.Count; i++)
                            {
                                Node TargetNode4 = NodeManagement.Get(Txn.TargetJobs[i].Position);
                                Job tmp;
                                TargetNode4.JobList.TryRemove(Txn.TargetJobs[i].Slot, out tmp);
                                tmp = new Job();
                                tmp.Job_Id = "No wafer";
                                tmp.Slot = Txn.TargetJobs[i].Slot;
                                TargetNode4.JobList.TryAdd(Txn.TargetJobs[i].Slot, tmp);
                                Txn.TargetJobs[i].LastNode = Txn.TargetJobs[i].Position;
                                Txn.TargetJobs[i].Position = Node.Name;
                                switch (i)
                                {
                                    case 0:
                                        Txn.TargetJobs[i].Slot = Txn.Arm;

                                        break;
                                    case 1:
                                        Txn.TargetJobs[i].Slot = Txn.Arm2;

                                        break;
                                }

                                Node.JobList.TryAdd(Txn.TargetJobs[i].Slot, Txn.TargetJobs[i]);

                                _EngReport.On_Job_Location_Changed(Txn.TargetJobs[i]);
                                // logger.Debug(JsonConvert.SerializeObject(Txn.TargetJobs[i]));
                            }

                            break;
                        case Transaction.Command.RobotType.Put:
                        case Transaction.Command.RobotType.PutWithoutBack:
                            //logger.Debug(Txn.TargetJobs.Count.ToString());



                            //Node.PreReady = true;


                            for (int i = 0; i < Txn.TargetJobs.Count; i++)
                            {
                                Job tmp;
                                Node.JobList.TryRemove(Txn.TargetJobs[i].Slot, out tmp);
                                Txn.TargetJobs[i].LastNode = Txn.TargetJobs[i].Position;
                                Txn.TargetJobs[i].Position = Txn.Position;
                                Txn.TargetJobs[i].ProcessFlag = true;
                                switch (i)
                                {
                                    case 0:
                                        Txn.TargetJobs[i].Slot = Txn.Slot;

                                        break;
                                    case 1:
                                        Txn.TargetJobs[i].Slot = Txn.Slot2;
                                        Txn.TargetJobs[i].Position = Txn.Position2;
                                        break;
                                }
                                Node TargetNode3 = NodeManagement.Get(Txn.TargetJobs[i].Position);
                                TargetNode3.JobList.TryRemove(Txn.TargetJobs[i].Slot, out tmp);
                                TargetNode3.JobList.TryAdd(Txn.TargetJobs[i].Slot, Txn.TargetJobs[i]);
                                _EngReport.On_Job_Location_Changed(Txn.TargetJobs[i]);
                                // logger.Debug(JsonConvert.SerializeObject(Txn.TargetJobs[i]));
                            }

                            break;


                        case Transaction.Command.RobotType.PutWait:


                            break;
                        case Transaction.Command.RobotType.PutBack:


                            break;
                    }
                    break;

            }

        }

        public void On_Command_TimeOut(Node Node, Transaction Txn)
        {
            logger.Debug("Transaction TimeOut:" + Txn.CommandEncodeStr);
            Node.State = "Alarm";
            _EngReport.On_Command_TimeOut(Node, Txn);
        }

        public void On_Event_Trigger(Node Node, ReturnMessage Msg)
        {
            try
            {
                logger.Debug("On_Event_Trigger");
                if (Msg.Command.Equals("ERROR"))
                {
                    Node.State = "Alarm";
                    _EngReport.On_Command_Error(Node, new Transaction(), Msg);
                }
                else
                {
                    switch (Node.Type)
                    {
                        case "LoadPort":

                            switch (Msg.Command)
                            {
                                case "MANSW":
                                    if (this.GetMode().Equals("Start"))
                                    {
                                        Node.ExcuteScript("LoadPortMapping", "MANSW", true);
                                        _EngReport.On_Node_State_Changed(Node, "Transfer Blocked");
                                    }
                                    break;
                                case "PODON":
                                    if (this.GetMode().Equals("Start"))
                                    {

                                        Node.ExcuteScript("LoadPortFoupIn", "LoadPortFoup", true);
                                    }
                                    _EngReport.On_Node_State_Changed(Node, "Transfer Readey");

                                    break;
                                case "PODOF":
                                    if (this.GetMode().Equals("Start"))
                                    {
                                        Node.ExcuteScript("LoadPortFoupOut", "LoadPortFoup", true);
                                    }
                                    _EngReport.On_Node_State_Changed(Node, "Transfer Ready");

                                    break;
                            }
                            break;
                    }

                    _EngReport.On_Event_Trigger(Node, Msg);
                }
            }
            catch (Exception e)
            {
                logger.Error(Node.Controller + "-" + Node.AdrNo + "(On_Command_Finished)" + e.Message + "\n" + e.StackTrace);
            }

        }

        public void On_Controller_State_Changed(string Device_ID, string Status)
        {

            logger.Debug(Device_ID + " " + Status);
            _EngReport.On_Controller_State_Changed(Device_ID, Status);
        }



        public void On_Node_State_Changed(Node Node, string Status)
        {
            Node.State = Status;
            _EngReport.On_Node_State_Changed(Node, Status);
        }

        public void On_Command_Error(Node Node, Transaction Txn, ReturnMessage Msg)
        {
            Node.State = "Alarm";
            _EngReport.On_Command_Error(Node, Txn, Msg);
            _EngReport.On_Node_State_Changed(Node, "Alarm");
        }

    }
}
