using log4net;
using TransferControl.Config;
using TransferControl.Controller;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TransferControl.Comm
{
    class ComPortClient : IConnection
    {
        private static readonly ILog logger = LogManager.GetLogger(typeof(ComPortClient));
        private SerialPort port;
        IConnectionReport ConnReport;
        DeviceConfig cfg;

        public ComPortClient(DeviceConfig _Config, IConnectionReport _ConnReport)
        {
            cfg = _Config;
            ConnReport = _ConnReport;
            Parity p = Parity.None;
            switch (_Config.ParityBit)
            {
                case "Even":
                    p = Parity.Even;
                    break;
                case "Mark":
                    p = Parity.Mark;
                    break;
                case "None":
                    p = Parity.None;
                    break;
                case "Odd":
                    p = Parity.Odd;
                    break;
                case "Space":
                    p = Parity.Space;
                    break;
            }
            StopBits s = StopBits.One;
            switch (_Config.StopBit)
            {
                case "None":
                    s = StopBits.None;
                    break;
                case "One":
                    s = StopBits.One;
                    break;
                case "OnePointFive":
                    s = StopBits.OnePointFive;
                    break;
                case "Two":
                    s = StopBits.Two;
                    break;
            }

            port = new SerialPort(_Config.PortName, _Config.BaudRate, p, _Config.DataBits, s);
        }
        public void Close()
        {
            port.Close();
            ConnReport.On_Connection_Disconnected("Close");
        }

        public void Connect()
        {

            Thread ComTd = new Thread(ConnectServer);
            ComTd.IsBackground = true;
            ComTd.Start();
        }

        public void Send(object Message)
        {
            try
            {

                port.Write(Message.ToString());
            }
            catch (Exception e)
            {
                //logger.Error("(ConnectServer " + RmIp + ":" + SPort + ")" + e.Message + "\n" + e.StackTrace);
                ConnReport.On_Connection_Error("(ConnectServer )" + e.Message + "\n" + e.StackTrace);
            }
        }

        private void ConnectServer()
        {

            try
            {
                ConnReport.On_Connection_Connecting("Connecting to ");
                port.Open();
                ConnReport.On_Connection_Connected("Connected! ");
                switch (cfg.Vendor.ToUpper())
                {
                    case "TDK":
                        port.DataReceived += new SerialDataReceivedEventHandler(TDK_DataReceived);
                        break;
                    case "SANWA":
                        port.DataReceived += new SerialDataReceivedEventHandler(Sanwa_DataReceived);
                        break;
                }


            }
            catch (Exception e)
            {
                //logger.Error("(ConnectServer " + RmIp + ":" + SPort + ")" + e.Message + "\n" + e.StackTrace);
                ConnReport.On_Connection_Error("(ConnectServer )" + e.Message + "\n" + e.StackTrace);
            }
        }



        private void TDK_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                string data = "";
                //switch (cfg.DeviceType)
                //{
                //    case "TDKController":
                //data = port.ReadTo("\r");

                //        break;
                //    case "SanwaController":
                data = port.ReadTo(((char)3).ToString());
                //        break;
                //}

                ThreadPool.QueueUserWorkItem(new WaitCallback(ConnReport.On_Connection_Message), data);
            }
            catch (Exception e1)
            {
                //logger.Error("(ConnectServer " + RmIp + ":" + SPort + ")" + e.Message + "\n" + e.StackTrace);
                ConnReport.On_Connection_Error("(TDK_DataReceived )" + e1.Message + "\n" + e1.StackTrace);
            }
        }

        private void Sanwa_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                string data = "";
                
                data = port.ReadTo("\r");
                

                ThreadPool.QueueUserWorkItem(new WaitCallback(ConnReport.On_Connection_Message), data);
            }
            catch (Exception e1)
            {
                //logger.Error("(ConnectServer " + RmIp + ":" + SPort + ")" + e.Message + "\n" + e.StackTrace);
                ConnReport.On_Connection_Error("(Sanwa_DataReceived )" + e1.Message + "\n" + e1.StackTrace);
            }
        }
    }
}
