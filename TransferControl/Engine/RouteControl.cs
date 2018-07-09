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
        int LapsedWfCount = 0;
        int LapsedLotCount = 0;

        public int SpinWaitTimeOut = 99999000;

        /// <summary>
        /// 建構子，傳入一個事件回報對象
        /// </summary>
        /// <param name="ReportTarget"></param>
        public RouteControl(IEngineReport ReportTarget)
        {
            _Mode = "Stop";
            _EngReport = ReportTarget;

            //初始化所有Controller
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

            //初始化所有Node
            ConfigTool<Node> NodeCfg = new ConfigTool<Node>();

            foreach (Node eachNode in NodeCfg.ReadFileByList("config/Node/Nodes.json"))
            {
                if (!eachNode.Enable)
                {
                    continue;
                }

                eachNode.InitialObject();
                NodeManagement.Add(eachNode.Name, eachNode);

            }
            //初始化傳送腳本
            PathManagement.LoadConfig();
            //初始化命令腳本
            CommandScriptManagement.LoadConfig();
            //初始化命令參數轉換表
            CmdParamManagement.Initialize();
        }
        /// <summary>
        /// 對所有Controller連線
        /// </summary>
        public void ConnectAll()
        {
            ControllerManagement.ConnectAll();
        }
        /// <summary>
        /// 對所有Controller斷線
        /// </summary>
        public void DisconnectAll()
        {
            ControllerManagement.DisonnectAll();
        }
        /// <summary>
        /// 整機暫停後啟動
        /// </summary>
        public void Continue()
        {
            if (_Mode.Equals("Pause"))
            {
                _Mode = "Start";
                foreach (Node node in NodeManagement.GetList())
                {
                    Transaction txn = new Transaction();
                    if (node.Type.Equals("Robot"))
                    {
                        txn.Method = Transaction.Command.RobotType.Continue;
                        node.SendCommand(txn);
                        node.State = "Run";
                    }
                    else if (node.Type.Equals("Aligner"))
                    {
                        txn.Method = Transaction.Command.AlignerType.Continue;
                        node.SendCommand(txn);
                        node.State = "Run";
                    }
                    txn.FormName = "PauseProcedure";
                }
            }
            else
            {
                throw new Exception("狀態錯誤:無法執行");
            }
        }
        /// <summary>
        /// 整機暫停
        /// </summary>
        public void Pause()
        {
            if (_Mode.Equals("Start"))
            {
                _Mode = "Pause";
                foreach (Node node in NodeManagement.GetList())
                {
                    Transaction txn = new Transaction();
                    if (node.Type.Equals("Robot"))
                    {
                        txn.Method = Transaction.Command.RobotType.Pause;
                        node.SendCommand(txn);
                        node.State = "Pause";
                    }
                    else if (node.Type.Equals("Aligner"))
                    {
                        txn.Method = Transaction.Command.AlignerType.Pause;
                        node.SendCommand(txn);
                        node.State = "Pause";
                    }
                    txn.FormName = "PauseProcedure";
                }
            }
            else
            {
                throw new Exception("狀態錯誤:尚未啟動");
            }
        }


        /// <summary>
        /// 整機Wafer搬送停止
        /// </summary>
        public void Stop()
        {
            lock (this)
            {

                _Mode = "Stop";

                IsInitial = false;
                foreach (Node port in NodeManagement.GetLoadPortList())
                {
                    port.Available = false;
                    port.Fetchable = false;
                    port.ReserveList.Clear();
                    foreach (Job j in port.JobList.Values.ToList())
                    {
                        j.Destination = "";
                        j.DestinationSlot = "";
                        j.DisplayDestination = "";
                    }
                    _EngReport.On_Mode_Changed("Stop");
                }
            }

        }
        /// <summary>
        /// 整機Wafer搬送開始
        /// </summary>
        /// <param name="FormName"></param>
        public void Start(string FormName)
        {
            lock (this)
            {
                if (_Mode != "Stop")
                {
                    throw new Exception("目前已在Start模式");
                }

                else
                {
                    _Mode = "Start";
                    //把所有殘餘命令清除
                    ControllerManagement.ClearTransactionList();
                    if (FormName.Equals("Running"))
                    {
                        _EngReport.On_Mode_Changed("Running");
                    }
                    else
                    {
                        _EngReport.On_Mode_Changed("Start");
                    }
                }
            }
            ThreadPool.QueueUserWorkItem(new WaitCallback(StartMonitor), FormName);
        }
        /// <summary>
        /// 監控可用Foup，並開始搬送
        /// </summary>
        /// <param name="FormName"></param>
        private void StartMonitor(object FormName)
        {

            while (!_Mode.Equals("Stop"))
            {
                while (true)
                {

                    logger.Debug("等待可用Foup中");
                    SpinWait.SpinUntil(() => (from LD in NodeManagement.GetLoadPortList()
                                              where LD.Available == true && (LD.Mode.Equals("LD") || LD.Mode.Equals("LU"))
                                              select LD).Count() != 0 || _Mode.Equals("Stop"), SpinWaitTimeOut);
                    if ((from LD in NodeManagement.GetLoadPortList()
                         where LD.Available == true
                         select LD).Count() != 0 || _Mode.Equals("Stop"))
                    {
                        if (_Mode.Equals("Stop"))
                        {
                            logger.Debug("結束Start模式");
                            return;
                        }
                        else
                        {
                            logger.Debug("可用Foup出現");
                            _EngReport.On_Eqp_State_Changed("Run");
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
                        if (eachNode.NodeType.Equals("LoadPort"))
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
                                   where port.Available && (port.Mode.Equals("LD") || port.Mode.Equals("LU"))
                                   select port;
                        if (findPort.Count() == 0)
                        {


                            if (_Mode.Equals("Stop"))
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
                            tmp[0].Used = true;
                            _EngReport.On_Port_Begin(tmp[0].Name, FormName.ToString());
                            logger.Debug(robot.Name + ":指定 " + tmp[0].Name + " 開始取片");
                            LapsedLotCount++;
                            LapsedWfCount += (from jb in tmp[0].JobList.Values
                                              where jb.MapFlag && !jb.ProcessFlag
                                              select jb).Count();
                        }
                        else
                        {
                            logger.Debug("RobotFetchMode " + robot.Name + " 找不到可以搬的Port");
                            robot.Phase = "2";
                            robot.GetAvailable = true;//標記目前Robot可以接受其他搬送命令 
                            
                            continue;
                        }
                    }

                    RobotFetchMode(robot, "Normal", FormName.ToString());
                }
                logger.Debug("等待搬運週期完成");
                StartTime = DateTime.Now;
                SpinWait.SpinUntil(() => CheckCycle() || _Mode.Equals("Stop"), SpinWaitTimeOut); //等待搬運週期完成
                TimeSpan diff = DateTime.Now - StartTime;
                logger.Info("Process Time: " + diff.TotalSeconds);
                _EngReport.On_Task_Finished(FormName.ToString(), diff.TotalSeconds.ToString(), LapsedWfCount, LapsedLotCount);
                logger.Debug("搬運週期完成，下個周期開始");
                _EngReport.On_Eqp_State_Changed("Idle");
                LapsedWfCount = 0;
                LapsedLotCount = 0;
            }
            logger.Debug("結束Start模式");
        }
        /// <summary>
        /// 搬運週期完成條件
        /// </summary>
        /// <returns></returns>
        private bool CheckCycle()
        {


            //bool a = (from jb in JobManagement.GetJobList()
            //          where jb.Position.IndexOf("LoadPort") != -1 && jb.MapFlag && !jb.ProcessFlag && !
            //          select jb).Count() == 0;
            bool b = (from port in NodeManagement.GetLoadPortList()
                      where port.Used
                      select port).Count() == 0;
            bool c = (from port in NodeManagement.GetLoadPortList()
                      where port.Available == true && port.Fetchable == true
                      select port).Count() == 0;

            return b & c;
        }
        /// <summary>
        /// 確認在席
        /// </summary>
        /// <param name="Node"></param>
        /// <returns></returns>
        private bool CheckPresent(Node Node)
        {
            bool a = (from jb in Node.JobList.Values
                      where jb.MapFlag == true
                      select jb).Count() != 0;

            return a;
        }
        /// <summary>
        /// 取得目前模式
        /// </summary>
        /// <returns></returns>
        public string GetMode()
        {
            string result = "";
            lock (this)
            {
                result = _Mode;
            }

            return result;
        }
        /// <summary>
        /// Phase1 取片模式
        /// </summary>
        /// <param name="RobotNode"></param>
        /// <param name="ScriptName"></param>
        /// <param name="FormName"></param>
        private void RobotFetchMode(Node RobotNode, string ScriptName, string FormName)
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
                    if (eachNode.NodeType.Equals("LoadPort"))
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
                                      where Job.ProcessFlag == false && Job.MapFlag
                                      select Job;

                        if (findJob.Count() == 0)
                        {
                            PortNode.Fetchable = false;
                            PortNode.Available = false;

                            logger.Debug("RobotFetchMode " + RobotNode.Name + " 找不到可以搬的Wafer");
                            RobotNode.Phase = "2";
                            RobotNode.GetAvailable = true;//標記目前Robot可以接受其他搬送命令 
                           

                            //因(FindNextJob())會產生兩支執行序，所以要確保場上都沒有WAFER才能觸發On_Port_Finished事件
                            if ((from Job in JobManagement.GetJobList()
                                 where Job.Position.IndexOf("Robot") != -1 || Job.Position.IndexOf("Aligner") != -1
                                 select Job).Count() == 0)
                            {
                                _EngReport.On_Port_Finished(PortNode.Name, FormName);
                            }
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
                                        eachJob.ProcessFlag = true;
                                        TargetJobs.Add(eachJob);
                                        RobotNode.CurrentLoadPort = PortNode.Name;
                                        //找到第一片
                                       
                                        if (eachJob.AlignerFlag)
                                        {
                                            NodeManagement.Get(RobotNode.DefaultAligner).UnLockByJob = eachJob.Job_Id;
                                        }
                                    }
                                    else
                                    {
                                        int diff = Convert.ToInt16(eachJob.Slot) - FirstSlot;
                                        if (diff == 1)
                                        {
                                            ConsecutiveSlot = true;
                                            eachJob.ProcessFlag = true;
                                            TargetJobs.Add(eachJob);

                                        }
                                        else
                                        {

                                            ConsecutiveSlot = false;
                                        }

                                       
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
                                    txn.FormName = FormName;
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
                                    txn.FormName = FormName;
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
                              where Job.ProcessFlag == false && Job.MapFlag
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
                            eachJob.ProcessFlag = true;
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
                    txn.FormName = FormName;
                    if (RobotNode.SendCommand(txn))
                    {
                        Node NextRobot = NodeManagement.GetNextRobot(TargetJobs[0].Destination);

                        NextRobot.WaitForCarryCount += 1;
                        // logger.Debug(NextRobot.Name + " WaitForCarryCount:" + NextRobot.Status.WaitForCarryCount);

                    }
                }
                else
                {
                    var find = from job in RobotNode.JobList.Values.ToList()
                               where job.AlignerFlag
                               select job;
                    if (find.Count() == 0)
                    {
                        RobotNode.Phase = "3";//目前為Aligner Bypass,進入放片階段
                        RobotPutMode(RobotNode, ScriptName, FormName);
                    }
                    else
                    {
                        //已沒有
                        RobotNode.Phase = "2";//進入處理階段
                        foreach (Job eachJob in RobotNode.JobList.Values.ToList())
                        {
                            eachJob.CurrentState = Job.State.WAIT_PUT;
                        }
                        FindNextJob(RobotNode, ScriptName, FormName);
                    }
                }


            }
            else if (RobotNode.JobList.Count == 2)//雙臂有片
            {
                var find = from job in RobotNode.JobList.Values.ToList()
                           where job.AlignerFlag
                           select job;

                if (find.Count() == 0)
                {
                    RobotNode.Phase = "3";//目前為Aligner Bypass,進入放片階段
                    RobotPutMode(RobotNode, ScriptName, FormName);
                }
                else
                {
                    RobotNode.Phase = "2";//進入處理階段
                    FindNextJob(RobotNode, ScriptName, FormName);
                }
            }

        }

        /// <summary>
        /// Phase2 處理階段開始，控制放片
        /// </summary>
        /// <param name="RobotNode"></param>
        /// <param name="ScriptName"></param>
        /// <param name="FormName"></param>
        private void FindNextJob(Node RobotNode, string ScriptName, string FormName)
        {
            try
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

                            each.ProcessNode = NodeManagement.Get(RobotNode.DefaultAligner).Name;
                            lastProcessNode = each.ProcessNode;
                        }
                        else
                        {
                            //each.ProcessNode = NodeManagement.GetAnotherAligner(lastProcessNode).Name;
                            each.ProcessNode = NodeManagement.Get(RobotNode.DefaultAligner).Name;
                        }
                        List<Job> TargetJobs = new List<Job>();
                        TargetJobs.Add(each);

                        foreach (Path eachPath in PathManagement.GetFinishPath(ScriptName, "WAIT_PROCESS", ""))
                        {
                            each.CurrentState = eachPath.ChangeToStatus;
                            foreach (Path.Action eachAction in eachPath.TodoList)
                            {

                                TodoAction(ScriptName, eachAction, TargetJobs, RobotNode, FormName);
                            }
                            break;
                        }

                    }
                }
                else
                {

                    logger.Debug("沒有東西可以做");

                }
            }
            catch (Exception e)
            {
                logger.Error("(FindNextJob)" + e.Message + "\n" + e.StackTrace);
            }
        }
        /// <summary>
        /// 取得Script設定的物件
        /// </summary>
        /// <param name="Position"></param>
        /// <param name="Job"></param>
        /// <param name="Node"></param>
        /// <returns></returns>
        private Node GetPosNode(string Position, Job Job, Node Node)
        {
            Node result = null;
            switch (Position)
            {
                case "Job.Position":
                    result = NodeManagement.Get(Job.Position);
                    break;
                case "ReserveAligner":

                    result = NodeManagement.Get(Node.DefaultAligner);
                    break;
                case "Aligner":
                    logger.Debug("Node.CurrentPosition:" + Node.CurrentPosition);
                    //result = NodeManagement.GetAligner(Node.CurrentPosition, Job.FromPort);
                    result = NodeManagement.Get(Job.ProcessNode);
                    break;
                case "OCR":
                    result = NodeManagement.GetOCRByAligner(Node);
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
                    Node Target;
                    if (Node.Type.Equals("Robot"))
                    {
                        Target = NodeManagement.Get(Node.DefaultAligner);
                    }
                    else
                    {
                        Target = Node;
                    }
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
        /// <summary>
        /// 取得手臂和Slot位置
        /// </summary>
        /// <param name="txn"></param>
        /// <param name="Node"></param>
        /// <param name="TargetJob"></param>
        private void GetArmSlot(ref Transaction txn, Node Node, Job TargetJob)
        {
            Node target = NodeManagement.Get(txn.Position);
            if (target != null)
            {
                switch (txn.Method)
                {
                    case Transaction.Command.RobotType.Put:
                    case Transaction.Command.RobotType.WaitBeforePut:
                    case Transaction.Command.RobotType.PutWithoutBack:
                        if (target.Type.Equals("LoadPort"))
                        {
                            txn.Slot = TargetJob.DestinationSlot;
                            txn.Arm = TargetJob.Slot;
                        }
                        else
                        {
                            txn.Slot = "1";//目前只有Aligner，寫死Slot 1
                            txn.Arm = TargetJob.Slot;//用目前在Robot上的Slot，就是手臂號碼
                        }
                        break;
                    case Transaction.Command.RobotType.PutBack:
                        txn.Slot = "1";//目前只有Aligner，寫死Slot 1
                        txn.Arm = Node.PutOutArm;
                        break;
                    case Transaction.Command.RobotType.GetWait:
                        txn.Arm = "1";//暫時都用上手臂去等
                        txn.Slot = "1";//目前只有Aligner，寫死Slot 1
                        break;
                    case Transaction.Command.RobotType.WaitBeforeGet:
                    case Transaction.Command.RobotType.Get:
                    case Transaction.Command.RobotType.GetAfterWait:
                        txn.Slot = TargetJob.Slot;
                        if (!Node.JobList.ContainsKey("1"))//找到Robot的空手臂，取片
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
        /// <summary>
        /// 傳送腳本
        /// </summary>
        /// <param name="ScriptName"></param>
        /// <param name="Action"></param>
        /// <param name="TargetJobs"></param>
        /// <param name="FinNode"></param>
        /// <param name="FormName"></param>
        private void TodoAction(string ScriptName, Path.Action Action, List<Job> TargetJobs, Node FinNode, string FormName)
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
                txn.FormName = FormName;
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
                            logger.Debug(Node.Name + " 等待Robot主控權 :" + Node.GetAvailable + "," + Node.InterLock + "," + Node.UnLockByJob + "," + TargetJob.Job_Id + "," + Node.JobList.Count);
                            SpinWait.SpinUntil(() => Node.Phase.Equals("2") && ((Node.GetAvailable && Node.GetMutex && Node.JobList.Count < 2) || (Node.GetAvailable && !Node.InterLock && (Node.UnLockByJob.Equals(TargetJob.Job_Id) || Node.UnLockByJob.Equals("")) && Node.JobList.Count < 2) || Force || !TargetJob.WaitToDo.Equals(Action.Method)) || _Mode.Equals("Stop"), SpinWaitTimeOut); //等待Robot有空
                            if (_Mode.Equals("Stop"))
                            {
                                logger.Debug("離開自動模式");
                                return;
                            }
                            //logger.Debug(JsonConvert.SerializeObject(Node));
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

                                    //logger.Debug(JsonConvert.SerializeObject(Node));
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
                                logger.Debug(Node.Name + " 等待主控權 " + Node.Available + "," + Node.JobList.Count + "," + TargetJob.Job_Id);
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
                        if (Action.Param == null)
                        {
                            Action.Param = "";
                        }
                        if (Action.Param.ToUpper().Equals("BYSETTING"))
                        {
                            Node NextRobot = NodeManagement.GetNextRobot(Node, TargetJob);
                            if (NextRobot != null)
                            {

                                var findRt = from rt in NextRobot.RouteTable
                                             where rt.NodeName.Equals(Node.Name)
                                             select rt;
                                if (findRt.Count() != 0)
                                {
                                    TargetJob.Offset = findRt.First().Offset;//Get aligner offset

                                    findRt = from rt in NextRobot.RouteTable
                                             where rt.NodeName.Equals(TargetJob.Destination)
                                             select rt;
                                    if (findRt.Count() != 0)
                                    {
                                        TargetJob.Offset += findRt.First().Offset; //Get UnloadPort offset
                                        TargetJob.Offset += TargetJob.Angle;
                                        txn.Value = TargetJob.Offset.ToString();
                                    }
                                    else
                                    {
                                        logger.Debug("Try to get Unload Port angle offset fail: " + TargetJob.Destination + " not found from " + NextRobot.Name + "'s route table.");
                                    }
                                }
                                else
                                {
                                    logger.Debug("Try to get Align angle offset fail: " + Node.Name + " not found from " + NextRobot.Name + "'s route table.");
                                }

                            }
                            else
                            {
                                logger.Debug("Try to get Align angle offset fail: NextRobot not found.");
                            }

                        }
                        else
                        {
                            txn.Value = Action.Param;
                        }
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
        /// <summary>
        ///Phase3 放Foup階段
        /// </summary>
        /// <param name="RobotNode"></param>
        /// <param name="ScriptName"></param>
        /// <param name="FormName"></param>
        private void RobotPutMode(Node RobotNode, string ScriptName, string FormName)
        {
            Job Wafer;
            List<Job> TargetJobs = new List<Job>();
            if (RobotNode.JobList.Count == 0)//雙臂皆空
            {

                RobotNode.Phase = "1";//進入取片階段
                RobotFetchMode(RobotNode, ScriptName, FormName);
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
                txn.FormName = FormName;
                RobotNode.SendCommand(txn);
            }
            else if (RobotNode.JobList.Count == 2)//雙臂有片
            {
                List<Job> Jobs = RobotNode.JobList.Values.ToList();
                Jobs.Sort((x, y) => { return Convert.ToInt16(x.DestinationSlot).CompareTo(Convert.ToInt16(y.DestinationSlot)); });
                if (Jobs[0].Destination.Equals(Jobs[1].Destination))
                {
                    int DestSlotDiff = Convert.ToInt16(Jobs[1].DestinationSlot) - Convert.ToInt16(Jobs[0].DestinationSlot);
                    Jobs.Sort((x, y) => { return Convert.ToInt16(x.Slot).CompareTo(Convert.ToInt16(y.Slot)); });
                    //上下手臂DestSlot順序相反問題
                    int SlotDiff = Convert.ToInt16(Jobs[1].DestinationSlot) - Convert.ToInt16(Jobs[0].DestinationSlot);

                    if (SlotDiff == 1 && DestSlotDiff == 1)
                    {//雙臂同放
                        Wafer = Jobs[1];
                        Transaction txn = new Transaction();
                        txn.TargetJobs = Jobs;
                        txn.Position = Wafer.Destination;
                        txn.Slot = (Convert.ToInt16(Wafer.DestinationSlot)).ToString();
                        txn.Method = Transaction.Command.RobotType.DoublePut;
                        txn.Arm = "";
                        txn.ScriptName = ScriptName;
                        txn.FormName = FormName;
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
                        txn.FormName = FormName;
                        RobotNode.SendCommand(txn);
                    }
                }


            }
        }



        /// <summary>
        /// 命令傳送成功
        /// </summary>
        /// <param name="Node"></param>
        /// <param name="Txn"></param>
        /// <param name="Msg"></param>
        public void On_Command_Excuted(Node Node, Transaction Txn, ReturnMessage Msg)
        {
            try
            {
                logger.Debug("On_Command_Excuted");

                //所有裝置
                switch (Txn.Method)
                {
                    case Transaction.Command.RobotType.Reset:
                        Node.State = Node.LastState;
                        _EngReport.On_Node_State_Changed(Node, Node.State);
                        break;
                }



                Job TargetJob = null;
                if (Txn.TargetJobs.Count != 0)
                {
                    TargetJob = Txn.TargetJobs[0];
                    if (Node.Type.Equals("Robot"))
                    {
                        Node.CurrentPosition = Txn.Position;
                    }//分裝置別
                    switch (Node.Type)
                    {
                        case "LoadPort":
                            switch (Txn.Method)
                            {


                                case Transaction.Command.LoadPortType.Unload:
                                case Transaction.Command.LoadPortType.MappingUnload:
                                case Transaction.Command.LoadPortType.DoorUp:
                                case Transaction.Command.LoadPortType.InitialPos:
                                case Transaction.Command.LoadPortType.ForceInitialPos:
                                    //LoadPort卸載時打開安全鎖
                                    Node.InterLock = true;
                                    //標記尚未Mapping
                                    Node.IsMapping = false;
                                    //刪除所有帳
                                    foreach (Job eachJob in Node.JobList.Values)
                                    {
                                        JobManagement.Remove(eachJob.Job_Id);
                                    }
                                    Node.JobList.Clear();
                                    Node.ReserveList.Clear();
                                    JobManagement.ClearAssignJobByPort(Node.Name);


                                    break;

                                case Transaction.Command.LoadPortType.ReadStatus:
                                    //偵測LoadPort門是否有開，沒開就停止所有可能會撞擊的裝置
                                    if (Txn.FormName.Equals("InterLockTxn"))
                                    {
                                        MessageParser parser = new MessageParser(Node.Brand);
                                        Dictionary<string, string> content = parser.ParseMessage(Txn.Method, Msg.Value);
                                        bool CheckResult = true;
                                        string info = "";
                                        foreach (KeyValuePair<string, string> each in content)
                                        {
                                            info += each.Key + ":" + each.Value + " ,";
                                            switch (each.Key)
                                            {

                                                case "Y Axis Position":
                                                    if (!each.Value.Equals("Dock position"))
                                                    {
                                                        CheckResult = false;
                                                    }
                                                    break;
                                                case "Door Position":
                                                    if (!each.Value.Equals("Open position"))
                                                    {
                                                        CheckResult = false;
                                                    }
                                                    break;

                                            }
                                        }
                                        logger.Debug(info);
                                        if (!CheckResult)
                                        {
                                            //檢查到LoadPort狀態不允許Robot存取
                                            var findRoute = from rt in Node.RouteTable
                                                            where rt.NodeType.Equals("Robot")
                                                            select rt;
                                            foreach (Node.Route rt in findRoute)
                                            {//暫停Robot所有動作
                                                Transaction StopTxn = new Transaction();
                                                StopTxn.Method = Transaction.Command.RobotType.Pause;
                                                StopTxn.FormName = "InterLockTxn";
                                                logger.Error("LoadPort " + Node.Name + " is not ready, send pause cmd to " + rt.NodeName + ".");
                                                NodeManagement.Get(rt.NodeName).SendCommand(StopTxn);
                                            }
                                        }
                                    }
                                    break;
                                case Transaction.Command.LoadPortType.GetMapping:
                                    //產生Mapping資料
                                    //string Mapping = Msg.Value;
                                    string Mapping = "1111000000000000000000000";
                                    //WaferAssignUpdate.UpdateLoadPortMapping(Node.Name, Msg.Value);
                                    int currentIdx = 1;
                                    for (int i = 0; i < Mapping.Length; i++)
                                    {
                                        Job wafer = new Job();
                                        wafer.Slot = (i + 1).ToString();
                                        wafer.FromPort = Node.Name;
                                        wafer.Position = Node.Name;
                                        wafer.AlignerFlag = false;
                                        string Slot = (i + 1).ToString("00");
                                        switch (Mapping[i])
                                        {
                                            case '0':
                                                wafer.Job_Id = "No wafer";
                                                wafer.Host_Job_Id = wafer.Job_Id;
                                                //MappingData.Add(wafer);
                                                break;
                                            case '1':
                                                while (true)
                                                {
                                                    wafer.Job_Id = "Wafer" + currentIdx.ToString("00");
                                                    wafer.Host_Job_Id = wafer.Job_Id;
                                                    wafer.MapFlag = true;
                                                    if (JobManagement.Add(wafer.Job_Id, wafer))
                                                    {

                                                        //MappingData.Add(wafer);
                                                        break;
                                                    }
                                                    currentIdx++;
                                                }

                                                break;
                                            case '2':
                                                wafer.Job_Id = "Crossed";
                                                wafer.Host_Job_Id = wafer.Job_Id;
                                                wafer.MapFlag = true;
                                                //MappingData.Add(wafer);
                                                break;
                                            case '?':
                                                wafer.Job_Id = "Undefined";
                                                wafer.Host_Job_Id = wafer.Job_Id;
                                                wafer.MapFlag = true;
                                                //MappingData.Add(wafer);
                                                break;
                                            case 'W':
                                                wafer.Job_Id = "Double";
                                                wafer.Host_Job_Id = wafer.Job_Id;
                                                wafer.MapFlag = true;
                                                //MappingData.Add(wafer);
                                                break;
                                        }
                                        if (!Node.AddJob(wafer.Slot, wafer))
                                        {
                                            Job org = Node.GetJob(wafer.Slot);
                                            JobManagement.Remove(org.Job_Id);
                                            Node.RemoveJob(wafer.Slot);
                                            Node.AddJob(wafer.Slot, wafer);
                                        }

                                    }
                                    Node.IsMapping = true;
                                    break;
                            }


                            break;
                        case "Robot":

                            if (!_Mode.Equals("Stop"))
                            {
                                switch (Node.Phase)
                                {
                                    case "1":

                                        break;
                                    case "2":

                                        //取得腳本的下一步
                                        foreach (Path eachPath in PathManagement.GetExcutePath(Txn.ScriptName, TargetJob.CurrentState, Txn.Method))
                                        {
                                            if (!eachPath.ChangeToStatus.Equals(""))
                                            {
                                                TargetJob.CurrentState = eachPath.ChangeToStatus;
                                            }
                                            foreach (Path.Action eachAction in eachPath.TodoList)
                                            {
                                                TodoAction(Txn.ScriptName, eachAction, Txn.TargetJobs, Node, Txn.FormName);
                                            }
                                            break;
                                        }

                                        break;
                                    case "3":

                                        break;
                                }

                            }
                            break;
                        case "Aligner":
                        case "OCR":

                            if (!_Mode.Equals("Stop"))
                            {
                                //TargetJob = Txn.TargetJobs[0];
                                foreach (Path eachPath in PathManagement.GetExcutePath(Txn.ScriptName, TargetJob.CurrentState, Txn.Method))
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
                                        TodoAction(Txn.ScriptName, eachAction, Txn.TargetJobs, Node, Txn.FormName);
                                    }
                                    break;
                                }
                            }
                            break;

                    }
                    _EngReport.On_Command_Excuted(Node, Txn, Msg);


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
            }
            catch (Exception e)
            {
                logger.Error(Node.Controller + "-" + Node.AdrNo + "(On_Command_Excuted)" + e.Message + "\n" + e.StackTrace);
            }
        }

        public void On_Command_Finished(Node Node, Transaction Txn, ReturnMessage Msg)
        {


            //var watch = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                Job TargetJob = null;
                if (Txn.TargetJobs.Count != 0)
                {
                    TargetJob = Txn.TargetJobs[0];
                    logger.Debug("On_Command_Finished:" + Txn.Method + ":" + Txn.Method);
                    switch (Node.Type)
                    {
                        case "Robot":
                            UpdateJobLocation(Node, Txn);
                            UpdateNodeStatus(Node, Txn);
                            if (!_Mode.Equals("Stop"))
                            {
                                switch (Node.Phase)
                                {
                                    case "1":
                                        //進入取片階段
                                        RobotFetchMode(Node, Txn.ScriptName, Txn.FormName);
                                        break;
                                    case "2":
                                        //進入處理階段
                                        //TargetJob = Txn.TargetJobs[0];
                                        List<Path> Paths = PathManagement.GetFinishPath(Txn.ScriptName, TargetJob.CurrentState, Txn.Method);

                                        //取得腳本的下一步
                                        foreach (Path eachPath in Paths)
                                        {
                                            if (!eachPath.ChangeToStatus.Equals(""))
                                            {
                                                TargetJob.CurrentState = eachPath.ChangeToStatus;
                                            }
                                            foreach (Path.Action eachAction in eachPath.TodoList)
                                            {
                                                TodoAction(Txn.ScriptName, eachAction, Txn.TargetJobs, Node, Txn.FormName);
                                            }
                                            break;
                                        }

                                        if ((Txn.Method.Equals(Transaction.Command.RobotType.Get) || Txn.Method.Equals(Transaction.Command.RobotType.GetAfterWait) || Txn.Method.Equals(Transaction.Command.RobotType.Put) || Txn.Method.Equals(Transaction.Command.RobotType.PutBack)) && ((Node.AllDone && Node.JobList.Count == 2) || (Node.AllDone && Node.WaitForCarryCount == 0)) && Node.Phase == "2" && Node.JobList.Count != 0)
                                        {//拿好拿滿就去放片吧
                                            logger.Debug("拿好拿滿就去放片吧");
                                            Node.Phase = "3";
                                            RobotPutMode(Node, Txn.ScriptName, Txn.FormName);
                                        }
                                        break;
                                    case "3":
                                        //進入放片階段
                                        RobotPutMode(Node, Txn.ScriptName, Txn.FormName);
                                        break;
                                }
                            }

                            break;
                        case "Aligner":
                        case "OCR":
                            UpdateNodeStatus(Node, Txn);
                            if (Node.Type.Equals("OCR"))
                            {
                                //Update Wafer ID by OCR result
                                if (Txn.Method.Equals(Transaction.Command.OCRType.Read))
                                {
                                    if (Txn.TargetJobs.Count != 0)
                                    {
                                        Txn.TargetJobs[0].Host_Job_Id = Msg.Value.Replace("[", "").Replace("]", "").Split(',')[0];
                                        _EngReport.On_Job_Location_Changed(Txn.TargetJobs[0]);
                                    }
                                }
                            }
                            if (!_Mode.Equals("Stop"))
                            {
                                //TargetJob = Txn.TargetJobs[0];
                                foreach (Path eachPath in PathManagement.GetFinishPath(Txn.ScriptName, TargetJob.CurrentState, Txn.Method))
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
                                        TodoAction(Txn.ScriptName, eachAction, Txn.TargetJobs, Node, Txn.FormName);
                                    }
                                    break;
                                }
                            }
                            else
                            {

                            }
                            break;
                        case "LoadPort":
                            UpdateNodeStatus(Node, Txn);
                            switch (Txn.Method)
                            {
                                case Transaction.Command.LoadPortType.MappingLoad:

                                    break;
                                case Transaction.Command.LoadPortType.Unload:
                                case Transaction.Command.LoadPortType.MappingUnload:
                                case Transaction.Command.LoadPortType.UnDock:

                                    _EngReport.On_Node_State_Changed(Node, "UnLoad Complete");
                                    break;
                                case Transaction.Command.LoadPortType.InitialPos:
                                case Transaction.Command.LoadPortType.ForceInitialPos:
                                    _EngReport.On_Node_State_Changed(Node, "Ready To Load");
                                    break;
                            }
                            break;
                    }
                    _EngReport.On_Command_Finished(Node, Txn, Msg);



                    if (!Node.Type.Equals("LoadPort"))//LoadPort 只能在Mapping完成後關閉安全鎖
                    {
                        Node.InterLock = false;
                    }
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
            }
            catch (Exception e)
            {
                logger.Error(Node.Controller + "-" + Node.AdrNo + "(On_Command_Finished)" + e.Message + "\n" + e.StackTrace);
            }
            //watch.Stop();
            //var elapsedMs = watch.ElapsedMilliseconds;
            //logger.Debug("On_Command_Finished ProcessTime:" + elapsedMs.ToString());




        }
        /// <summary>
        /// 更新Node狀態
        /// </summary>
        /// <param name="Node"></param>
        /// <param name="Txn"></param>
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
                                //4port use only
                                Node.PutAvailable = true;
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
                                    RobotFetchMode(Node, Txn.ScriptName, Txn.FormName);
                                }

                            }

                            break;
                        case Transaction.Command.RobotType.WaitBeforeGet:
                        case Transaction.Command.RobotType.PutWithoutBack:
                            Node.PutOutArm = Txn.Arm;
                            Node.PutOut = true;
                            //4Port use only

                            Node.GetAvailable = true;
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

                            Node.InterLock = false;
                            _EngReport.On_Node_State_Changed(Node, "Load Complete");
                            break;
                        case Transaction.Command.LoadPortType.Unload:
                            _EngReport.On_Node_State_Changed(Node, "UnLoad Complete");
                            break;
                        default:
                            Node.InterLock = true;
                            break;
                    }
                    break;
            }
            //logger.Debug(JsonConvert.SerializeObject(Node));

        }
        /// <summary>
        /// 更新Wafer位置
        /// </summary>
        /// <param name="Node"></param>
        /// <param name="Txn"></param>
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
                                //LoadPort 空的Slot要塞假資料
                                if (TargetNode5.Type.Equals("LoadPort"))
                                {
                                    tmp = new Job();
                                    tmp.Job_Id = "No wafer";
                                    tmp.Host_Job_Id = "No wafer";
                                    tmp.Slot = Txn.TargetJobs[i].Slot;
                                    TargetNode5.JobList.TryAdd(Txn.TargetJobs[i].Slot, tmp);
                                }
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
                            //if (IsTaskFinish())
                            //{
                            //    _EngReport.On_Task_Finished(Txn.FormName);
                            //}
                            break;
                        case Transaction.Command.RobotType.Get://更新Wafer位置
                        case Transaction.Command.RobotType.GetAfterWait:

                            //logger.Debug(Txn.TargetJobs.Count.ToString());
                            for (int i = 0; i < Txn.TargetJobs.Count; i++)
                            {
                                Node TargetNode4 = NodeManagement.Get(Txn.TargetJobs[i].Position);
                                Job tmp;
                                TargetNode4.JobList.TryRemove(Txn.TargetJobs[i].Slot, out tmp);
                                //LoadPort 空的Slot要塞假資料
                                if (TargetNode4.Type.Equals("LoadPort"))
                                {
                                    tmp = new Job();
                                    tmp.Job_Id = "No wafer";
                                    tmp.Host_Job_Id = "No wafer";
                                    tmp.Slot = Txn.TargetJobs[i].Slot;
                                    TargetNode4.JobList.TryAdd(Txn.TargetJobs[i].Slot, tmp);
                                }
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
                                //Txn.TargetJobs[i].ProcessFlag = true;
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
                            //if (Txn.Method.Equals(Transaction.Command.RobotType.Put))
                            //{
                            //    if (IsTaskFinish())
                            //    {
                            //        _EngReport.On_Task_Finished(Txn.FormName);
                            //    }
                            //}
                            break;


                        case Transaction.Command.RobotType.PutWait:


                            break;
                        case Transaction.Command.RobotType.PutBack:


                            break;
                    }
                    break;

            }

        }
        /// <summary>
        /// 命令超時
        /// </summary>
        /// <param name="Node"></param>
        /// <param name="Txn"></param>
        public void On_Command_TimeOut(Node Node, Transaction Txn)
        {
            logger.Debug("Transaction TimeOut:" + Txn.CommandEncodeStr);
            if (!Node.State.Equals("Alarm"))
            {
                Node.LastState = Node.State;
            }
            Node.State = "Alarm";
            _EngReport.On_Command_TimeOut(Node, Txn);
        }
        /// <summary>
        /// 事件觸發
        /// </summary>
        /// <param name="Node"></param>
        /// <param name="Msg"></param>
        public void On_Event_Trigger(Node Node, ReturnMessage Msg)
        {
            try
            {
                logger.Debug("On_Event_Trigger");
                if (Msg.Command.Equals("ERROR"))
                {
                    if (!Node.State.Equals("Alarm"))
                    {
                        Node.LastState = Node.State;
                    }
                    Node.State = "Alarm";
                    _EngReport.On_Command_Error(Node, new Transaction(), Msg);
                    _EngReport.On_Node_State_Changed(Node, "Alarm");
                }
                else
                {
                    _EngReport.On_Event_Trigger(Node, Msg);
                }
            }
            catch (Exception e)
            {
                logger.Error(Node.Controller + "-" + Node.AdrNo + "(On_Command_Finished)" + e.Message + "\n" + e.StackTrace);
            }

        }
        /// <summary>
        /// Controller狀態變更
        /// </summary>
        /// <param name="Device_ID"></param>
        /// <param name="Status"></param>
        public void On_Controller_State_Changed(string Device_ID, string Status)
        {

            logger.Debug(Device_ID + " " + Status);
            _EngReport.On_Controller_State_Changed(Device_ID, Status);
        }


        /// <summary>
        /// Node機況變更
        /// </summary>
        /// <param name="Node"></param>
        /// <param name="Status"></param>
        public void On_Node_State_Changed(Node Node, string Status)
        {
            if (!Node.State.Equals("Alarm") && Status.Equals("Alarm"))
            {
                Node.LastState = Node.State;
            }
            StateRecord.NodeStateUpdate(Node.Name, Node.State, Status);
            Node.State = Status;
            
            _EngReport.On_Node_State_Changed(Node, Status);
        }
        /// <summary>
        /// 命令執行發生錯誤
        /// </summary>
        /// <param name="Node"></param>
        /// <param name="Txn"></param>
        /// <param name="Msg"></param>
        public void On_Command_Error(Node Node, Transaction Txn, ReturnMessage Msg)
        {
            if (!Node.State.Equals("Alarm"))
            {
                Node.LastState = Node.State;
            }
            Node.State = "Alarm";
            _EngReport.On_Command_Error(Node, Txn, Msg);
            _EngReport.On_Node_State_Changed(Node, "Alarm");
        }

    }
}
