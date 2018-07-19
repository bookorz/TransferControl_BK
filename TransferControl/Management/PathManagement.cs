using log4net;
using Newtonsoft.Json;
using SANWA.Utility;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TransferControl.Config;

namespace TransferControl.Management
{
    public class PathManagement
    {

        public static Dictionary<string, List<Path>> ScriptList;
        static ILog logger = LogManager.GetLogger(typeof(PathManagement));

        private static DBUtil dBUtil = new DBUtil();
        public static void LoadConfig()
        {
            ScriptList = new Dictionary<string, List<Path>>();
            string Sql = @"select * from config_transfer_script";
            DataTable dt = dBUtil.GetDataTable(Sql, null);
            string str_json = JsonConvert.SerializeObject(dt, Formatting.Indented);
            str_json = str_json.Replace("\"[", "[").Replace("]\"", "]").Replace("\\\"", "\"").Replace("\\r\\n","");
            List<Path> cmdScpList = JsonConvert.DeserializeObject<List<Path>>(str_json);
            List<Path> tmp;

            foreach (Path each in cmdScpList)
            {
                if (ScriptList.TryGetValue(each.ID, out tmp))
                {
                    tmp.Add(each);
                }
                else
                {
                    tmp = new List<Path>();
                    tmp.Add(each);
                    ScriptList.Add(each.ID, tmp);
                }


            }
        }
        public static List<Path> GetFinishPath(string ScriptName, string JobStatus, string FinishMethod)
        {
            List<Path> result = new List<Path>();

            if (ScriptList.TryGetValue(ScriptName, out result))
            {


                var findPath = from path in result
                               where path.JobStatus.Equals(JobStatus) && path.FinishMethod.Equals(FinishMethod)
                               select path;
                if (findPath.Count() != 0)
                {
                    result = findPath.ToList();
                }
                else
                {
                    findPath = from path in result
                               where path.JobStatus.Equals("") && path.FinishMethod.Equals(FinishMethod)
                               select path;

                    if (findPath.Count() != 0)
                    {
                        result = findPath.ToList();
                    }
                    else
                    {
                        result = new List<Path>();
                    }
                }
            }



            return result;
        }

        public static List<Path> GetExcutePath(string ScriptName, string JobStatus, string ExcuteMethod)
        {
            List<Path> result = new List<Path>();
            if (ScriptList.TryGetValue(ScriptName, out result))
            {
                var findPath = from path in result
                               where path.JobStatus.Equals(JobStatus) && path.ExcuteMethod.Equals(ExcuteMethod)
                               select path;
                if (findPath.Count() != 0)
                {
                    result = findPath.ToList();
                }
                else
                {
                    findPath = from path in result
                               where path.JobStatus.Equals("") && path.ExcuteMethod.Equals(ExcuteMethod)
                               select path;

                    if (findPath.Count() != 0)
                    {
                        result = findPath.ToList();
                    }
                    else
                    {
                        result = new List<Path>();
                    }
                }
            }
            return result;
        }
    }
}
