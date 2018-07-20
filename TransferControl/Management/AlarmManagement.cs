using log4net;
using Newtonsoft.Json;
using SANWA.Utility;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TransferControl.Management
{
    public class AlarmManagement
    {
        static ILog logger = LogManager.GetLogger(typeof(AlarmManagement));
        private static List<AlarmInfo> AlarmList = new List<AlarmInfo>();
        //private static List<AlarmInfo> AlarmHistory = new List<AlarmInfo>();

        public static void Add(AlarmInfo Alm)
        {
            AlarmList.Add(Alm);
            InsertToDB(Alm);

        }

        public static void Clear()
        {
            AlarmList.Clear();
        }

        public static bool HasCritical()
        {
            var find = from Alm in AlarmList.ToList()
                       where Alm.IsStop == true
                       select Alm;

            if (find.Count() != 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public static List<AlarmInfo> GetAll()
        {
            List<AlarmInfo> result = null;
            result = AlarmList.ToList();
            return result;
        }

        public static List<AlarmInfo> GetHistory()
        {
            return GetHistory(DateTime.Now.AddDays(-1),DateTime.Now);
        }

        public static List<AlarmInfo> GetHistory(DateTime From, DateTime To)
        {
            List<AlarmInfo> result = null;

            DBUtil dBUtil = new DBUtil();
            Dictionary<string, object> keyValues = new Dictionary<string, object>();
            DataTable dtTemp;

            try
            {
                string SQL = @"SELECT * from log_alarm_his t 
                                where t.time_stamp between
                                STR_TO_DATE(@From, '%Y-%m-%d %H:%i:%s') and
                                STR_TO_DATE(@To, '%Y-%m-%d %H:%i:%s')";
                keyValues.Add("@From", From.ToString("yyyy/MM/dd HH:mm:ss"));
                keyValues.Add("@To", To.ToString("yyyy/MM/dd HH:mm:ss"));
                dtTemp = dBUtil.GetDataTable(SQL, keyValues);

                string str_json = JsonConvert.SerializeObject(dtTemp, Formatting.Indented);
                result = JsonConvert.DeserializeObject<List<AlarmInfo>>(str_json);

            }
            catch (Exception e)
            {
                logger.Error("GetUUID error:" + e.StackTrace);
            }

            return result;
        }

        public static void Remove(string NodeName)
        {
            var find = from Alm in AlarmList.ToList()
                       where Alm.NodeName.Equals(NodeName)
                       select Alm;

            if (find.Count() != 0)
            {
                foreach (AlarmInfo each in find)
                {
                    AlarmList.Remove(each);
                }
            }


        }

        public static void Remove(AlarmInfo alm)
        {

            AlarmList.Remove(alm);

        }

        public static void InsertToDB(AlarmInfo alm)
        {

            DBUtil dBUtil = new DBUtil();
            Dictionary<string, object> keyValues = new Dictionary<string, object>();

            try
            {
                string SQL = @"insert into log_alarm_his (node_name,system_alarm_code,alarm_code,alarm_desc,alarm_eng_desc,alarm_type,is_stop,need_reset,time_stamp)
                                values(@node_name,@system_alarm_code,@alarm_code,@alarm_desc,@alarm_eng_desc,@alarm_type,@is_stop,@need_reset,@time_stamp)";

                keyValues.Add("@node_name", alm.NodeName);
                keyValues.Add("@system_alarm_code", alm.SystemAlarmCode);
                //keyValues.Add("@alarm_tpye", alm.AlarmType);
                keyValues.Add("@alarm_code", alm.AlarmCode);
                keyValues.Add("@alarm_desc", alm.Desc);
                keyValues.Add("@alarm_eng_desc", alm.EngDesc);
                keyValues.Add("@alarm_type", alm.Type);
                keyValues.Add("@is_stop", alm.IsStop);
                keyValues.Add("@need_reset", alm.NeedReset);
                keyValues.Add("@time_stamp", alm.TimeStamp.ToString("yyyy-MM-dd HH:mm:ss.ffffff"));
                

                dBUtil.ExecuteNonQueryAsync(SQL, keyValues);
                

            }
            catch (Exception e)
            {
                logger.Error("InsertToDB error:" + e.StackTrace);
            }


        }
    }
}
