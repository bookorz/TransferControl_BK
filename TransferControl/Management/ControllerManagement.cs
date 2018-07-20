using TransferControl.Controller;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using log4net;
using SANWA.Utility;
using System.Data;
using Newtonsoft.Json;

namespace TransferControl.Management
{
    public static class ControllerManagement
    {
        static ILog logger = LogManager.GetLogger(typeof(ControllerManagement));
        
        private static DBUtil dBUtil = new DBUtil();
        private static ConcurrentDictionary<string, DeviceController> Controllers;

        public static void LoadConfig(ICommandReport Report)
        {
            if (Controllers != null)
            {
                foreach(DeviceController each in Controllers.Values)
                {
                    each.Close();
                }
            }
            Controllers = new ConcurrentDictionary<string, DeviceController>();
            string Sql = @"select UPPER(t.node_function_name) as DeviceName,t.node_function_type as DeviceType,
                            case when t.conn_type = 'Socket' then  t.conn_address else '' end as IPAdress ,
                            case when t.conn_type = 'Socket' then  CONVERT(t.conn_prot,SIGNED) else 0 end as Port ,
                            case when t.conn_type = 'Comport' then   CONVERT(t.conn_prot,SIGNED) else 0 end as BaudRate ,
                            case when t.conn_type = 'Comport' then  t.conn_address else '' end as PortName ,
                            t.com_parity_bit as ParityBit,
                            ifnull(CONVERT(t.com_data_bits,SIGNED),0) as DataBits,
                            t.com_stop_bit as StopBit,
                            t.conn_type as ConnectionType,
                            t.enable_flg as Enable
                            from config_controller t
                            where t.controller_type = 'Equipment'";
            DataTable dt = dBUtil.GetDataTable(Sql, null);
            string str_json = JsonConvert.SerializeObject(dt, Formatting.Indented);

            List<DeviceConfig> ctrlList = JsonConvert.DeserializeObject<List<DeviceConfig>>(str_json);
           

            foreach (DeviceConfig each in ctrlList)
            {
                if (each.Enable)
                {
                    DeviceController tmp = new DeviceController(each, Report);
                    Controllers.TryAdd(each.DeviceName, tmp);
                }
            }
        }

        public static DeviceController Get(string Name)
        {
            DeviceController result = null;

            Controllers.TryGetValue(Name.ToUpper(), out result);

            return result;
        }
        public static bool Add(string Name, DeviceController Controller)
        {
            bool result = false;


            if (!Controllers.ContainsKey(Name))
            {
                Controllers.TryAdd(Name, Controller);
                result = true;
            }

            return result;
        }

        public static void ConnectAll()
        {
            foreach (DeviceController each in Controllers.Values.ToList())
            {
                if (!each._Config.DeviceType.Equals("HST")&& !each._Config.DeviceType.Equals("COGNEX"))
                {
                    each.Connect();
                }
            }
        }

        public static void DisonnectAll()
        {
            foreach (DeviceController each in Controllers.Values.ToList())
            {
                each.Close();
            }
        }

        public static bool CheckAllConnection()
        {
            var find = from ctrl in Controllers.Values.ToList()
                       where !ctrl.Status.Equals("Connected") && ctrl._Config.Enable
                       select ctrl;

            if (find.Count() == 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public static void ClearTransactionList()
        {
            var find = from ctrl in Controllers.Values.ToList()
                       where ctrl._Config.Enable
                       select ctrl;

            foreach(DeviceController Ctrl in find)
            {
                Ctrl.ClearTransactionList();
            }
        }
    }
}
