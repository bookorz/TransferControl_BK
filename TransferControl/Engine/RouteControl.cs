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

namespace TransferControl.Engine
{
    public class RouteControl : AlarmMapping, ICommandReport
    {
        private static readonly ILog logger = LogManager.GetLogger(typeof(RouteControl));
        string _Mode = "";
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

                DeviceController ctrl = new DeviceController(eachDevice, this);
                //ctrl.Connect();
                ControllerManagement.Add(eachDevice.DeviceName, ctrl);
            }
            ConfigTool<Node> NodeCfg = new ConfigTool<Node>();

            foreach (Node eachNode in NodeCfg.ReadFileByList("config/Node/Nodes.json"))
            {
                eachNode.Initial();
                NodeManagement.Add(eachNode.Name, eachNode);

            }

        }

        public void ConnectAll()
        {
            ControllerManagement.ConnectAll();
        }

        public void DisconnectAll()
        {
            ControllerManagement.DisonnectAll();
        }

        public void Stop()
        {
            lock (this)
            {
                _Mode = "Stop";
            }

        }

        public void Auto(object ScriptName)
        {
            lock (this)
            {
                if (_Mode == "Auto")
                {
                    throw new Exception("目前已在Auto模式");
                }
                else
                {
                    _Mode = "Auto";
                }
            }

            StartTime = DateTime.Now;
            foreach (Node robot in NodeManagement.GetEnableRobotList())
            {
                robot.Initial();

                RobotFetchMode(robot, ScriptName.ToString());
            }


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
                {//當沒有Port正在作業時，指定Load的時間最早的Port可以開始取片

                    findPort = from port in PortList
                               where port.Available
                               select port;
                    if (findPort.Count() == 0)
                    {

                        SpinWait.SpinUntil(() => (from port in PortList
                                                  where port.Available
                                                  select port).Count() != 0 || !_Mode.Equals("Auto"), SpinWaitTimeOut);
                        if (!_Mode.Equals("Auto"))
                        {
                            return;
                        }
                        findPort = from port in PortList
                                   where port.Available
                                   select port;
                    }

                    List<Node> tmp = findPort.ToList();

                    tmp.Sort((x, y) => { return x.LoadTime.CompareTo(y.LoadTime); });

                    PortList[0].Fetchable = true;
                    logger.Debug(RobotNode.Name + ":指定 " + PortList[0].Name + " 開始取片");
                }


                foreach (Node PortNode in PortList)
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
                            logger.Debug("RobotFetchMode " + RobotNode.Name + " 找不到可以搬的Wafer");
                            RobotNode.Phase = "2";
                            RobotNode.GetAvailable = true;//標記目前Robot可以接受其他搬送命令 
                            TimeSpan diff = DateTime.Now - StartTime;
                            logger.Info("Process Time: " + diff.TotalSeconds);
                            _EngReport.On_Port_Finished(PortNode.Name);

                        }
                        else
                        {
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


                                        }
                                        else
                                        {

                                            ConsecutiveSlot = false;
                                        }
                                        TargetJobs.Add(eachJob);
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
                    SpinWait.SpinUntil(() => RobotNode.PutAvailable, SpinWaitTimeOut); //等待可以放片時機
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

                                SpinWait.SpinUntil(() => !Node.PutOut, SpinWaitTimeOut); //等待Robot手臂收回
                                logger.Debug(Node.Name + " 偵測到手臂收回，離開等待，需求命令:" + Action.EqpType + ":" + Action.Method);
                            }
                            if (Target.JobList.Count != 0 && (Action.Method.Equals(Transaction.Command.RobotType.Put) || Action.Method.Equals(Transaction.Command.RobotType.PutWithoutBack)))
                            {
                                logger.Debug(Node.Name + " 偵測到目標在席存在中，等待，需求命令:" + Action.EqpType + ":" + Action.Method);
                                SpinWait.SpinUntil(() => Target.JobList.Count == 0, SpinWaitTimeOut);
                                logger.Debug(Node.Name + " 偵測到目標在席已被取走，離開等待，需求命令:" + Action.EqpType + ":" + Action.Method);
                            }

                            logger.Debug(Node.Name + " 等待主控權 " + Action.EqpType + ":" + Action.Method + ":" + TargetJob.Job_Id);
                            logger.Debug(JsonConvert.SerializeObject(Node));
                            SpinWait.SpinUntil(() => (!Node.InterLock && (Node.UnLockByJob.Equals(TargetJob.Job_Id) || Node.UnLockByJob.Equals(""))) || Force, SpinWaitTimeOut); //等待Robot有空
                            lock (Node)
                            {

                                if (Target.JobList.Count != 0 && (Action.Method.Equals(Transaction.Command.RobotType.Put) || Action.Method.Equals(Transaction.Command.RobotType.PutWithoutBack)))
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
                            SpinWait.SpinUntil(() => Node.Phase.Equals("2") && ((Node.GetAvailable && Node.GetMutex && Node.JobList.Count < 2) || (Node.GetAvailable && !Node.InterLock && (Node.UnLockByJob.Equals(TargetJob.Job_Id) || Node.UnLockByJob.Equals("")) && Node.JobList.Count < 2) || Force || !TargetJob.WaitToDo.Equals(Action.Method)), SpinWaitTimeOut); //等待Robot有空
                            logger.Debug(JsonConvert.SerializeObject(Node));
                            logger.Debug("TargetJob.Job_Id:" + TargetJob.Job_Id);
                            if (Node.PutOut && !Action.Method.Equals(Transaction.Command.RobotType.GetAfterWait) && !Action.Method.Equals(Transaction.Command.RobotType.PutBack) && !Action.Method.Equals(Transaction.Command.RobotType.Get))
                            {
                                logger.Debug(Node.Name + " 偵測到手臂伸出中，等待收回，需求命令:" + Action.EqpType + ":" + Action.Method);

                                SpinWait.SpinUntil(() => !Node.PutOut, SpinWaitTimeOut); //等待Robot手臂收回
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

                            SpinWait.SpinUntil(() => (!Node.InterLock && (Node.UnLockByJob.Equals("") || Node.UnLockByJob.Equals(TargetJob.Job_Id))) || Force, SpinWaitTimeOut);

                            if (Action.Method.Equals(Transaction.Command.AlignerType.WaferHold))
                            {
                                logger.Debug(Node.Name + " 偵測到目標節點未就緒，等待，需求命令:" + Action.EqpType + ":" + Action.Method);

                                SpinWait.SpinUntil(() => Node.Available, SpinWaitTimeOut);

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
                if (Node.Type.Equals("Robot"))
                {
                    Node.CurrentPosition = Txn.Position;
                }
                if (_Mode.Equals("Auto"))
                {
                    switch (Node.Phase)
                    {
                        case "1":

                            break;
                        case "2":
                            Job TargetJob = Txn.TargetJobs[0];

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
                Job TargetJob;
                logger.Debug("On_Command_Finished:" + Txn.Method + ":" + Txn.Method);
                Node.InterLock = false;


                switch (Node.Type)
                {
                    case "Robot":
                        UpdateJobLocation(Node, Txn);
                        UpdateNodeStatus(Node, Txn);
                        if (_Mode.Equals("Auto"))
                        {
                            switch (Node.Phase)
                            {
                                case "1":
                                    RobotFetchMode(Node, Txn.ScriptName);
                                    break;
                                case "2":
                                    TargetJob = Txn.TargetJobs[0];

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

                                    if ((Txn.Method.Equals(Transaction.Command.RobotType.Get) || Txn.Method.Equals(Transaction.Command.RobotType.GetAfterWait) || Txn.Method.Equals(Transaction.Command.RobotType.Put) || Txn.Method.Equals(Transaction.Command.RobotType.PutBack)) && ((Node.AllDone && Node.JobList.Count == 2) || (Node.AllDone && Node.WaitForCarryCount == 0)) && Node.Phase == "2")
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
                        TargetJob = Txn.TargetJobs[0];
                        UpdateNodeStatus(Node, Txn);
                        if (_Mode.Equals("Auto"))
                        {
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
                    case "OCR":

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
            }
            logger.Debug(JsonConvert.SerializeObject(Node));

        }

        private void UpdateJobLocation(Node Node, Transaction Txn)
        {
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
                                Txn.TargetJobs[i].LastNode = Txn.TargetJobs[i].Position;
                                Txn.TargetJobs[i].Slot = (i + 1).ToString();
                                Txn.TargetJobs[i].Position = Node.Name;
                                Node.JobList.TryAdd(Txn.TargetJobs[i].Slot, Txn.TargetJobs[i]);

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
                                TargetNode6.JobList.TryAdd(Txn.TargetJobs[i].Slot, Txn.TargetJobs[i]);

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
                                TargetNode3.JobList.TryAdd(Txn.TargetJobs[i].Slot, Txn.TargetJobs[i]);

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
            _EngReport.On_Command_TimeOut(Node, Txn);
        }

        public void On_Event_Trigger(Node Node, ReturnMessage Msg)
        {
            try
            {
                logger.Debug("On_Event_Trigger");
                _EngReport.On_Event_Trigger(Node, Msg);

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
            _EngReport.On_Node_State_Changed(Node, Status);
        }

        public void On_Command_Error(Node Node, Transaction Txn, ReturnMessage Msg)
        {
            _EngReport.On_Command_Error(Node, Txn, Msg);
        }

    }
}
