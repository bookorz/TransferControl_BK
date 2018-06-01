using TransferControl.Comm;
using TransferControl.Config;
using TransferControl.Management;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


using log4net;
using System.Collections.Concurrent;
using SANWA.Utility;
using System.Threading;

namespace TransferControl.Controller
{
    public class DeviceController : IController, IConnectionReport, ITransactionReport
    {
        private static readonly ILog logger = LogManager.GetLogger(typeof(DeviceController));
        ICommandReport _ReportTarget;
        IConnection conn;
        DeviceConfig _Config;
        SANWA.Utility.Decoder _Decoder;
        ConcurrentDictionary<string, Transaction> TransactionList = new ConcurrentDictionary<string, Transaction>();
        public string Name { get; set; }
        public string Status { get; set; }

        public DeviceController(DeviceConfig Config, ICommandReport ReportTarget)
        {
            _ReportTarget = ReportTarget;
            _Config = Config;

            switch (Config.ConnectionType)
            {
                case "Socket":
                    conn = new SocketClient(Config.IPAdress, Config.Port, this);
                    break;
                case "ComPort":
                    conn = new ComPortClient(Config, this);
                    break;

            }
            _Decoder = new SANWA.Utility.Decoder(Config.DeviceType);
            this.Name = _Config.DeviceName;

        }
        public void Close()
        {
            try
            {
                conn.Close();
            }
            catch (Exception e)
            {

                logger.Error(_Config.DeviceName + "(DisconnectServer " + _Config.IPAdress + ":" + _Config.Port.ToString() + ")" + e.Message + "\n" + e.StackTrace);

            }
        }

        public void Connect()
        {
            try
            {
                conn.Connect();
            }
            catch (Exception e)
            {
                logger.Error(_Config.DeviceName + "(ConnectToServer " + _Config.IPAdress + ":" + _Config.Port.ToString() + ")" + e.Message + "\n" + e.StackTrace);
            }
        }

        public bool DoWork(Transaction Txn)
        {

            bool result = false;
            // lock (TransactionList)
            if (!Txn.NodeType.Equals("OCR"))
            {
                List<ReturnMessage> msgList = _Decoder.GetMessage(Txn.CommandEncodeStr);
                if (msgList.Count != 0)
                {
                    Txn.Type = msgList[0].Command;
                    Txn.CommandType = msgList[0].CommandType;
                }
            }
            else
            {
                Txn.Type = "";
                //Txn.CommandType = "";
            }
            if (TransactionList.TryAdd(Txn.AdrNo + Txn.Type, Txn))
            {

                Txn.SetTimeOutReport(this);
                Txn.SetTimeOutMonitor(true);

                conn.Send(Txn.CommandEncodeStr);
                string waferids = "";
                foreach (Job each in Txn.TargetJobs)
                {
                    waferids += each.Job_Id + " ";
                }
                logger.Debug(_Config.DeviceName + " Send:" + Txn.CommandEncodeStr.Replace("\r", "") + " Wafer:" + waferids);
                result = true;

            }
            else
            {
                Transaction workingTxn;
                TransactionList.TryGetValue(Txn.AdrNo + Txn.Type, out workingTxn);
                logger.Debug(_Config.DeviceName + "(DoWork " + _Config.IPAdress + ":" + _Config.Port.ToString() + ":" + Txn.CommandEncodeStr + ") Same type command " + workingTxn.CommandEncodeStr + " is already excuting.");

                result = false;
            }



            //}
            return result;
        }

        public void On_Connection_Message(object MsgObj)
        {
            try
            {
                string Msg = (string)MsgObj;
                logger.Debug(_Config.DeviceName + " Recieve:" + Msg.Replace("\r", ""));

                List<ReturnMessage> ReturnMsgList = _Decoder.GetMessage(Msg);
                foreach (ReturnMessage ReturnMsg in ReturnMsgList)
                {
                    try
                    {
                        Transaction Txn = null;
                        Node Node;
                        if (ReturnMsg != null)
                        {
                            Node = NodeManagement.GetByController(_Config.DeviceName, ReturnMsg.NodeAdr);
                            //lock (TransactionList)
                            //{
                            lock (Node)
                            {
                                if (ReturnMsg.Type == ReturnMessage.ReturnType.Event)
                                {
                                    //_ReportTarget.On_Event_Trigger(Node, ReturnMsg);
                                }
                                else if (TransactionList.TryRemove(ReturnMsg.NodeAdr + ReturnMsg.Command, out Txn))
                                {

                                    switch (ReturnMsg.Type)
                                    {
                                        case ReturnMessage.ReturnType.Excuted:
                                            if (!Txn.CommandType.Equals("CMD") && !Txn.CommandType.Equals("MOV"))
                                            {
                                                logger.Debug("Txn timmer stoped.");
                                                Txn.SetTimeOutMonitor(false);
                                            }
                                            else
                                            {
                                                Txn.SetTimeOutMonitor(false);
                                                Txn.SetTimeOut(30000);
                                                Txn.SetTimeOutMonitor(true);
                                                TransactionList.TryAdd(ReturnMsg.NodeAdr + ReturnMsg.Command, Txn);
                                            }
                                            //_ReportTarget.On_Command_Excuted(Node, Txn, ReturnMsg);
                                            break;
                                        case ReturnMessage.ReturnType.Finished:
                                            logger.Debug("Txn timmer stoped.");
                                            Txn.SetTimeOutMonitor(false);
                                            //_ReportTarget.On_Command_Finished(Node, Txn, ReturnMsg);
                                            break;
                                        case ReturnMessage.ReturnType.Error:
                                            logger.Debug("Txn timmer stoped.");
                                            Txn.SetTimeOutMonitor(false);
                                            //_ReportTarget.On_Command_Error(Node, Txn, ReturnMsg);
                                            break;
                                        case ReturnMessage.ReturnType.Information:
                                            logger.Debug("Txn timmer stoped.");
                                            Txn.SetTimeOutMonitor(false);

                                            ReturnMsg.Type = ReturnMessage.ReturnType.Finished;
                                            //SpinWait.SpinUntil(() => false, 300);
                                            ThreadPool.QueueUserWorkItem(new WaitCallback(conn.Send), ReturnMsg.FinCommand);
                                            logger.Debug(_Config.DeviceName + "Send:" + ReturnMsg.FinCommand);
                                            break;
                                    }
                                }
                                else
                                {
                                    if (ReturnMsg.Type.Equals(ReturnMessage.ReturnType.Information))
                                    {
                                        ThreadPool.QueueUserWorkItem(new WaitCallback(conn.Send), ReturnMsg.FinCommand);
                                        logger.Debug(_Config.DeviceName + "Send:" + ReturnMsg.FinCommand);
                                    }
                                    else
                                    {
                                        logger.Debug(_Config.DeviceName + "(On_Connection_Message Txn is not found. msg:" + Msg);
                                        return;
                                    }
                                }
                            }

                            switch (ReturnMsg.Type)
                            {
                                case ReturnMessage.ReturnType.Information:
                                case ReturnMessage.ReturnType.Event:
                                    _ReportTarget.On_Event_Trigger(Node, ReturnMsg);
                                    break;
                                case ReturnMessage.ReturnType.Excuted:

                                    _ReportTarget.On_Command_Excuted(Node, Txn, ReturnMsg);
                                    if (Txn.CommandType.Equals("CMD") && !Node.Type.Equals("LoadPort"))
                                    {
                                        _ReportTarget.On_Node_State_Changed(Node, "RUN");
                                    }
                                    break;
                                case ReturnMessage.ReturnType.Finished:

                                    _ReportTarget.On_Command_Finished(Node, Txn, ReturnMsg);
                                    if (!Node.Type.Equals("LoadPort"))
                                    {
                                        _ReportTarget.On_Node_State_Changed(Node, "IDLE");
                                    }
                                    break;
                                case ReturnMessage.ReturnType.Error:

                                    _ReportTarget.On_Command_Error(Node, Txn, ReturnMsg);

                                    break;

                            }

                            //}
                        }
                        else
                        {
                            logger.Debug(_Config.DeviceName + "(On_Connection_Message Message decode fail:" + Msg);
                        }
                    }
                    catch (Exception e)
                    {
                        logger.Error(_Config.DeviceName + "(On_Connection_Message " + _Config.IPAdress + ":" + _Config.Port.ToString() + ")" + e.Message + "\n" + e.StackTrace);
                    }
                }
            }
            catch (Exception e)
            {
                logger.Error(_Config.DeviceName + "(On_Connection_Message " + _Config.IPAdress + ":" + _Config.Port.ToString() + ")" + e.Message + "\n" + e.StackTrace);
            }
        }

        public void On_Connection_Connected(string Msg)
        {
            _ReportTarget.On_Controller_State_Changed(_Config.DeviceName, "Connected");
            this.Status = "Connected";
        }

        public void On_Connection_Connecting(string Msg)
        {
            _ReportTarget.On_Controller_State_Changed(_Config.DeviceName, "Connecting");
            this.Status = "Connecting";
        }

        public void On_Connection_Disconnected(string Msg)
        {
            _ReportTarget.On_Controller_State_Changed(_Config.DeviceName, "Disconnected");
            this.Status = "Disconnected";
        }

        public void On_Connection_Error(string Msg)
        {
            foreach (Transaction txn in TransactionList.Values.ToList())
            {
                txn.SetTimeOutMonitor(false);
            }
            TransactionList.Clear();
            _ReportTarget.On_Controller_State_Changed(_Config.DeviceName, "Connection_Error");
        }

        public void On_Transaction_TimeOut(Transaction Txn)
        {
            logger.Debug(_Config.DeviceName + "(On_Transaction_TimeOut Txn is timeout:" + Txn.CommandEncodeStr);
            Txn.SetTimeOutMonitor(false);
            if (TransactionList.TryRemove(Txn.AdrNo + Txn.Type, out Txn))
            {
                Node Node = NodeManagement.GetByController(_Config.DeviceName, Txn.AdrNo);
                if (Node != null)
                {
                    _ReportTarget.On_Command_TimeOut(Node, Txn);
                }
                else
                {
                    logger.Debug(_Config.DeviceName + "(On_Transaction_TimeOut Get Node fail.");
                }
            }
            else
            {
                logger.Debug(_Config.DeviceName + "(On_Transaction_TimeOut TryRemove Txn fail.");
            }
        }
    }
}
