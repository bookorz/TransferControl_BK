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

        //委托
        private delegate void delSocketDataArrival(byte[] data);
        delSocketDataArrival socketDataArrival;

        private delegate void delSocketDisconnected();
        delSocketDisconnected socketDisconnected;

        public Socket theSocket = null;
        private string remoteHost = "192.168.1.71";
        private int remotePort = 6666;

        private string SockErrorStr = null;
        private ManualResetEvent TimeoutObject = new ManualResetEvent(false);
        private bool IsconnectSuccess = false; //異步連接情況，由異步連接回調函數置位
        private object lockObj_IsConnectSuccess = new object();
        IConnectionReport ConnReport;
        DeviceConfig Config;
        int RDataLen = 100;  //固定長度傳送資料~ 可以針對自己的需要改長度 
        ///

        /// 構造函數
        /// 
        /// 
        /// 
        public SocketClient(DeviceConfig _Config, IConnectionReport _ConnReport)
        {
            Config = _Config;
            remoteHost = _Config.IPAdress;
            remotePort = _Config.Port;
            ConnReport = _ConnReport;

            socketDataArrival = socketDataArrivalHandler;
            socketDisconnected = socketDisconnectedHandler;

        }

        public void Start()
        {
            checkSocketState();

        }

        /// 設置心跳
        /// 
        private void SetHeartBeat()
        {
            //byte[] inValue = new byte[] { 1, 0, 0, 0, 0x20, 0x4e, 0, 0, 0xd0, 0x07, 0, 0 };// 首次探測時間20 秒, 間隔偵測時間2 秒
            byte[] inValue = new byte[] { 1, 0, 0, 0, 0x88, 0x13, 0, 0, 0xd0, 0x07, 0, 0 };// 首次探測時間5 秒, 間隔偵測時間2 秒
            theSocket.IOControl(IOControlCode.KeepAliveValues, inValue, null);
        }

        ///

        /// 創建套接字+異步連接函數
        /// 
        /// 
        private bool socket_create_connect()
        {
            IPAddress ipAddress = IPAddress.Parse(remoteHost);
            IPEndPoint remoteEP = new IPEndPoint(ipAddress, remotePort);
            theSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            theSocket.SendTimeout = 1000;

            SetHeartBeat();//設置心跳參數

            #region 異步連接代碼

            TimeoutObject.Reset(); //覆位timeout事件
            try
            {
                ConnReport.On_Connection_Connecting("Connecting");
                theSocket.BeginConnect(remoteEP, connectedCallback, theSocket);
            }
            catch (Exception err)
            {
                SockErrorStr = err.ToString();
                ConnReport.On_Connection_Error(err.Message);
                return false;
            }
            if (TimeoutObject.WaitOne(10000, false))//直到timeout，或者TimeoutObject.set()
            {
                if (IsconnectSuccess)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                SockErrorStr = "Time Out";
                ConnReport.On_Connection_Error("Time Out");
                return false;
            }
            #endregion
        }

        ///

        /// 同步receive函數
        /// 
        /// 
        /// 
        private string socket_receive(byte[] readBuffer)
        {
            try
            {
                if (theSocket == null)
                {
                    socket_create_connect();
                }
                else if (!theSocket.Connected)
                {
                    if (!IsSocketConnected())
                        Reconnect();
                }

                int bytesRec = theSocket.Receive(readBuffer);

                if (bytesRec == 0)
                {
                    //warning 0 bytes received
                }
                return Encoding.ASCII.GetString(readBuffer, 0, bytesRec);
            }
            catch (SocketException se)
            {
                //print se.ErrorCode
                throw;
            }
        }


       

        ///

        /// 同步send函數
        /// 
        /// 
        /// 
        public bool Send(object sendMessage)
        {
            if (checkSocketState())
            {
                return SendData(sendMessage);
            }
            return false;
        }

        ///

        /// 斷線重連函數
        /// 
        /// 
        private bool Reconnect()
        {
            //關閉socket
            theSocket.Shutdown(SocketShutdown.Both);

            theSocket.Disconnect(true);
            IsconnectSuccess = false;

            theSocket.Close();

            //創建socket
            return socket_create_connect();
        }

        ///

        /// 當socket.connected為false時，進一步確定下當前連接狀態
        /// 
        /// 
        private bool IsSocketConnected()
        {
            #region remarks
            /********************************************************************************************
             * 當Socket.Conneted為false時， 如果您需要確定連接的當前狀態，請進行非阻塞、零字節的 Send 調用。
             * 如果該調用成功返回或引發 WAEWOULDBLOCK 錯誤代碼 (10035)，則該套接字仍然處於連接狀態； 
             * 否則，該套接字不再處於連接狀態。
             * Depending on http://msdn.microsoft.com/zh-cn/library/system.net.sockets.socket.connected.aspx?cs-save-lang=1&cs-lang=csharp#code-snippet-2
            ********************************************************************************************/
            #endregion

            #region 過程
            // This is how you can determine whether a socket is still connected.
            bool connectState = true;
            bool blockingState = theSocket.Blocking;
            try
            {
                byte[] tmp = new byte[1];

                theSocket.Blocking = false;
                theSocket.Send(tmp, 0, 0);
                //Console.WriteLine("Connected!");
                connectState = true; //若Send錯誤會跳去執行catch體，而不會執行其try體裏其之後的代碼
            }
            catch (SocketException e)
            {
                // 10035 == WSAEWOULDBLOCK
                if (e.NativeErrorCode.Equals(10035))
                {
                    //Console.WriteLine("Still Connected, but the Send would block");
                    connectState = true;
                }

                else
                {
                    //Console.WriteLine("Disconnected: error code {0}!", e.NativeErrorCode);
                    connectState = false;
                }
            }
            finally
            {
                theSocket.Blocking = blockingState;
            }

            //Console.WriteLine("Connected: {0}", client.Connected);
            return connectState;
            #endregion
        }

        ///

        /// 另一種判斷connected的方法，但未檢測對端網線斷開或ungraceful的情況
        /// 
        /// 
        /// 
        public bool IsSocketConnected(Socket s)
        {
            #region remarks
            /* As zendar wrote, it is nice to use the Socket.Poll and Socket.Available, but you need to take into consideration 
             * that the socket might not have been initialized in the first place. 
             * This is the last (I believe) piece of information and it is supplied by the Socket.Connected property. 
             * The revised version of the method would looks something like this: 
             * from：http://stackoverflow.com/questions/2661764/how-to-check-if-a-socket-is-connected-disconnected-in-c */
            #endregion

            #region 過程

            if (s == null)
                return false;
            return !((s.Poll(1000, SelectMode.SelectRead) && (s.Available == 0)) || !s.Connected);

            /* The long, but simpler-to-understand version:
                    bool part1 = s.Poll(1000, SelectMode.SelectRead);
                    bool part2 = (s.Available == 0);
                    if ((part1 && part2 ) || !s.Connected)
                        return false;
                    else
                        return true;
            */
            #endregion
        }

        ///

        /// 異步連接回調函數
        /// 
        /// 
        void connectedCallback(IAsyncResult iar)
        {
            #region <remarks>
            /// 1、置位IsconnectSuccess
            #endregion </remarks>

            lock (lockObj_IsConnectSuccess)
            {
                ConnReport.On_Connection_Connected("Connected");
                Socket client = (Socket)iar.AsyncState;
                try
                {
                    client.EndConnect(iar);
                    IsconnectSuccess = true;
                    StartKeepAlive(); //開始KeppAlive檢測
                }
                catch (Exception e)
                {
                    //Console.WriteLine(e.ToString());
                    SockErrorStr = e.ToString();
                    IsconnectSuccess = false;
                }
                finally
                {
                    TimeoutObject.Set();
                }
            }
        }

        ///

        /// 開始KeepAlive檢測函數
        /// 
        private void StartKeepAlive()
        {
            theSocket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, new AsyncCallback(OnReceiveCallback), theSocket);
        }

        ///

        /// BeginReceive回調函數
        /// 
        byte[] buffer = new byte[1024];
        private void OnReceiveCallback(IAsyncResult ar)
        {
            try
            {
                Socket peerSock = (Socket)ar.AsyncState;
                int BytesRead = peerSock.EndReceive(ar);
                if (BytesRead > 0)
                {
                    byte[] tmp = new byte[BytesRead];
                    Array.ConstrainedCopy(buffer, 0, tmp, 0, BytesRead);
                    if (socketDataArrival != null)
                    {
                        socketDataArrival(tmp);
                    }
                }
                else//對端gracefully關閉一個連接
                {
                    if (theSocket.Connected)//上次socket的狀態
                    {
                        if (socketDisconnected != null)
                        {
                            //1-重連
                            socketDisconnected();
                            //2-退出，不再執行BeginReceive
                            return;
                        }
                    }
                }
                //此處buffer似乎要清空--待實現 zq
                for(int i = 0; i < buffer.Length; i++)// Initial buffer
                {
                    buffer[i] = 0;
                }
                theSocket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, new AsyncCallback(OnReceiveCallback), theSocket);
            }
            catch (Exception ex)
            {
                if (socketDisconnected != null)
                {
                    socketDisconnected(); //Keepalive檢測網線斷開引發的異常在這裏捕獲
                    return;
                }
            }
        }

        ///
        string S = "";
        /// 異步收到消息處理器
        /// 
        /// 
        private void socketDataArrivalHandler(byte[] OrgData)
        {


            byte[] clientData = new byte[RDataLen];
            string data = "";
            switch (Config.Vendor.ToUpper())
            {
                case "TDK":


                    S += Encoding.Default.GetString(OrgData, 0, OrgData.Length);
                    if (S.IndexOf(Convert.ToChar(3)) != -1)
                    {
                        //logger.Debug("s:" + S);
                        data = S.Substring(0, S.IndexOf(Convert.ToChar(3)) + 1);
                        //logger.Debug("data:" + data);

                        S = S.Substring(S.IndexOf(Convert.ToChar(3)) + 1);
                        //logger.Debug("s:" + S);
                        ThreadPool.QueueUserWorkItem(new WaitCallback(ConnReport.On_Connection_Message), data);
                        break;
                    }


                    break;
                case "SANWA":

                    S += Encoding.Default.GetString(OrgData, 0, OrgData.Length);

                    if (S.IndexOf("\r") != -1)
                    {
                        //logger.Debug("s:" + S);
                        data = S.Substring(0, S.IndexOf("\r"));
                        //logger.Debug("data:" + data);

                        S = S.Substring(S.IndexOf("\r") + 1);
                        //logger.Debug("s:" + S);
                        ThreadPool.QueueUserWorkItem(new WaitCallback(ConnReport.On_Connection_Message), data);
                        break;
                    }


                    break;
                default:
                    S += Encoding.Default.GetString(OrgData, 0, OrgData.Length);
                    data = S;
                    ThreadPool.QueueUserWorkItem(new WaitCallback(ConnReport.On_Connection_Message), data);
                    S = "";
                    break;
            }




        }

        ///

        /// socket由於連接中斷(軟/硬中斷)的後續工作處理器
        /// 
        private void socketDisconnectedHandler()
        {
            Reconnect();
        }

        ///

        /// 檢測socket的狀態
        /// 
        /// 
        public bool checkSocketState()
        {
            try
            {
                if (theSocket == null)
                {
                    return socket_create_connect();
                }
                else if (IsconnectSuccess)
                {
                    return true;
                }
                else//已創建套接字，但未connected
                {
                    #region 異步連接代碼

                    TimeoutObject.Reset(); //覆位timeout事件
                    try
                    {
                        IPAddress ipAddress = IPAddress.Parse(remoteHost);
                        IPEndPoint remoteEP = new IPEndPoint(ipAddress, remotePort);
                        theSocket.BeginConnect(remoteEP, connectedCallback, theSocket);

                        SetHeartBeat();//設置心跳參數
                    }
                    catch (Exception err)
                    {
                        SockErrorStr = err.ToString();
                        return false;
                    }
                    if (TimeoutObject.WaitOne(2000, false))//直到timeout，或者TimeoutObject.set()
                    {
                        if (IsconnectSuccess)
                        {
                            return true;
                        }
                        else
                        {
                            return false;
                        }
                    }
                    else
                    {
                        SockErrorStr = "Time Out";
                        return false;
                    }

                    #endregion
                }

            }
            catch (SocketException se)
            {
                SockErrorStr = se.ToString();
                return false;
            }
        }


        ///

        /// 同步發送
        /// 
        /// 
        /// 
        public bool SendData(object data)
        {
            bool result = false;
            string dataStr = data.ToString();
            if (dataStr == null || dataStr.Length < 0)
                return result;
            try
            {
                byte[] t = new byte[Encoding.ASCII.GetByteCount(dataStr.ToString())]; ;
                int i = Encoding.ASCII.GetBytes(dataStr.ToString(), 0, Encoding.ASCII.GetByteCount(dataStr.ToString()), t, 0);

                //byte[] cmd = Encoding.Default.GetBytes(dataStr);
                int n = theSocket.Send(t);
                if (n < 1)
                    result = false;
            }
            catch (Exception ee)
            {
                SockErrorStr = ee.ToString();
                result = false;
            }
            return result;
        }
    }
}