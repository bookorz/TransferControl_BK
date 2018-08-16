using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TransferControl.Controller;

namespace TransferControl.Comm
{
    class SocketClient : IConnection
    {
        private static readonly ILog logger = LogManager.GetLogger(typeof(SocketClient));
        Socket SckSPort = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp); // 先行宣告Socket

        string RmIp = "192.168.0.127";  // 其中 xxx.xxx.xxx.xxx 為Server端的IP

        int SPort = 23;

        int RDataLen = 100;  //固定長度傳送資料~ 可以針對自己的需要改長度 
        DeviceConfig cfg;



        IConnectionReport ConnReport;

        public SocketClient(DeviceConfig _Config, IConnectionReport _ConnReport)
        {
            RmIp = _Config.IPAdress;
            SPort = _Config.Port;
            ConnReport = _ConnReport;
            cfg = _Config;

            
        }

        // 連線

        public void Connect()
        {
            //if (SckSPort != null)
            //{
            //    if (SckSPort.Connected)
            //    {
            //        SckSPort.Close();
            //    }
            //}
            Thread SckTd = new Thread(SckSReceiveProc);
            SckTd.IsBackground = true;
            SckTd.Start();

            SckTd = new Thread(TryConnect);
            SckTd.IsBackground = true;
            SckTd.Start();
        }

        private void TryConnect()
        {
            while (true)
            {
                if (!SckSPort.Connected)
                {
                    try
                    {
                        logger.Debug("Try connecting to " + cfg.IPAdress + ":" + cfg.Port);
                        
                        SckSPort.Connect(new IPEndPoint(IPAddress.Parse(RmIp), SPort));
                        ThreadPool.QueueUserWorkItem(new WaitCallback(ConnReport.On_Connection_Connected), "Connected");
                        //ConnReport.On_Connection_Connected("Connected");
                        return;
                    }
                    catch (Exception e)
                    {
                        logger.Error(e.StackTrace, e);
                        try
                        {
                            SckSPort.Close();
                        }
                        catch
                        {

                        }
                    }
                }
                SpinWait.SpinUntil(() => false, 6000);
            }
        }

        string S = "";
        private void SckSReceiveProc()
        {

            try
            {

                int IntAcceptData;

                byte[] clientData = new byte[RDataLen];
                string data = "";
                bool isReported = true;
                while (true)
                {
                    if (!SckSPort.Connected)
                    {
                        //logger.Error(Desc + " (" + RmIp + ":" + SPort + ") is disconnected.");
                        if (!isReported)
                        {
                            ConnReport.On_Connection_Disconnected("(" + RmIp + ":" + SPort + ") is disconnected.");
                            isReported = true;
                        }
                        SpinWait.SpinUntil(() => false, 50);
                    }
                    else
                    {
                        // 程式會被 hand 在此, 等待接收來自 Server 端傳來的資料
                        isReported = false;


                        // 往下就自己寫接收到來自Server端的資料後要做什麼事唄~^^”

                        switch (cfg.Vendor.ToUpper())
                        {
                            case "TDK":
                                while (true)
                                {
                                    IntAcceptData = SckSPort.Receive(clientData);
                                    S += Encoding.Default.GetString(clientData, 0, IntAcceptData);
                                    if (S.IndexOf(Convert.ToChar(3)) != -1)
                                    {
                                        //logger.Debug("s:" + S);
                                        data = S.Substring(0, S.IndexOf(Convert.ToChar(3)) + 1);
                                        //logger.Debug("data:" + data);

                                        S = S.Substring(S.IndexOf(Convert.ToChar(3)) + 1);
                                        //logger.Debug("s:" + S);
                                        break;
                                    }

                                }
                                break;
                            case "SANWA":
                                while (true)
                                {
                                    IntAcceptData = SckSPort.Receive(clientData);
                                    S += Encoding.Default.GetString(clientData, 0, IntAcceptData);
                                    if (S.IndexOf("\r") != -1)
                                    {
                                        //logger.Debug("s:" + S);
                                        data = S.Substring(0, S.IndexOf("\r"));
                                        //logger.Debug("data:" + data);

                                        S = S.Substring(S.IndexOf("\r") + 1);
                                        //logger.Debug("s:" + S);
                                        break;
                                    }
                                }

                                break;
                            default:
                                IntAcceptData = SckSPort.Receive(clientData);
                                S = Encoding.Default.GetString(clientData, 0, IntAcceptData);
                                data = S;
                                S = "";
                                break;
                        }



                        ThreadPool.QueueUserWorkItem(new WaitCallback(ConnReport.On_Connection_Message), data);
                        //ConnReport.On_Connection_Message(S);


                        //Console.WriteLine(S);
                        //logger.Info("[Rev<--]" + S.Replace("\n", "") + "(From " + Desc + " " + RmIp + ":" + SPort + ")");
                    }
                }

            }

            catch (Exception e)
            {
                logger.Error("(From " + RmIp + ":" + SPort + ")" + e.Message + "\n" + e.StackTrace);
                //ConnReport.On_Connection_Disconnected("SckSReceiveProc (" + RmIp + ":" + SPort + ")" + e.Message + "\n" + e.StackTrace);

            }
        }


        // 當然 Client 端也可以傳送資料給Server端~ 和 Server 端的SckSSend一樣, 只差在Client端只有一個Socket

        public void Send(object Msg)

        {
            try

            {
                //SckSPort.Send(Msg);
                //logger.Info("[Snd-->]" + Msg.Replace("\r", "") + "(To " + Desc + " " + RmIp + ":" + SPort + ")");
                byte[] t = new byte[Encoding.ASCII.GetByteCount(Msg.ToString())]; ;
                int i = Encoding.ASCII.GetBytes(Msg.ToString(), 0, Encoding.ASCII.GetByteCount(Msg.ToString()), t, 0);
                if (SckSPort.Connected == true)
                {
                    SckSPort.Send(t);
                }
            }

            catch (Exception e)
            {
                logger.Error("(To " + RmIp + ":" + SPort + ")" + e.Message + "\n" + e.StackTrace);
                //ConnReport.On_Connection_Error("Send (" + RmIp + ":" + SPort + ")" + e.Message + "\n" + e.StackTrace);
            }





        }

        public void Close()
        {
            //if (SckSPort != null)
            //{

            //    SckSPort.Close();


            //}
        }
    }
}
