using log4net;
using Newtonsoft.Json;
using SANWA.Utility;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TransferControl.Config;

namespace TransferControl.Management
{
    public class CommandScriptManagement
    {
        ILog logger = LogManager.GetLogger(typeof(CommandScriptManagement));
        static ConcurrentDictionary<string, List<CommandScript>> CommandScriptList = new ConcurrentDictionary<string, List<CommandScript>>();
        private static DBUtil dBUtil = new DBUtil();
        public static void LoadConfig()
        {


            string Sql = @"SELECT * FROM config_command_script";
            DataTable dt = dBUtil.GetDataTable(Sql, null);
            string str_json = JsonConvert.SerializeObject(dt, Formatting.Indented);

            List<CommandScript> cmdScpList = JsonConvert.DeserializeObject<List<CommandScript>>(str_json);
            List<CommandScript> tmp;

            foreach (CommandScript each in cmdScpList)
            {
                if (CommandScriptList.TryGetValue(each.CommandScriptID, out tmp))
                {
                    tmp.Add(each);
                }
                else
                {
                    tmp = new List<CommandScript>();
                    tmp.Add(each);
                    CommandScriptList.TryAdd(each.CommandScriptID, tmp);
                }


            }

        }

        public static void ReloadScriptWithParam(string ScriptName, Dictionary<string, string> Param)
        {
            Dictionary<string, object> keyValues = new Dictionary<string, object>();
            string Sql = @"SELECT * FROM config_command_script where CommandScriptID = @scriptname";
            keyValues.Add("@scriptname", ScriptName);
            DataTable dt = dBUtil.GetDataTable(Sql, keyValues);
            string str_json = JsonConvert.SerializeObject(dt, Formatting.Indented);
            foreach (string eachP in Param.Keys)
            {
                string val = "";
                Param.TryGetValue(eachP, out val);
                str_json = str_json.Replace(eachP, val);
            }
            List<CommandScript> cmdScpList = JsonConvert.DeserializeObject<List<CommandScript>>(str_json);
            List<CommandScript> tmp;
            CommandScriptList.TryRemove(ScriptName, out tmp);

            foreach (CommandScript each in cmdScpList)
            {
                

                if (CommandScriptList.TryGetValue(each.CommandScriptID, out tmp))
                {
                    tmp.Add(each);
                }
                else
                {
                    tmp = new List<CommandScript>();
                    tmp.Add(each);
                    CommandScriptList.TryAdd(each.CommandScriptID, tmp);
                }


            }

        }

        public static CommandScript GetStart(string ScriptName)
        {
            List<CommandScript> result = new List<CommandScript>();

            if (CommandScriptList.TryGetValue(ScriptName, out result))
            {
                var findPath = from Command in result
                               where Command.FinishMethod.Equals("") && Command.ExcuteMethod.Equals("")
                               select Command;
                if (findPath.Count() != 0)
                {
                    result = findPath.ToList();
                }
                else
                {
                    result = new List<CommandScript>();
                }
            }
            return result.First();
        }

        public static List<CommandScript> GetFinishNext(string ScriptName, string FinishMethod)
        {
            List<CommandScript> result = new List<CommandScript>();

            if (CommandScriptList.TryGetValue(ScriptName, out result))
            {


                var findPath = from Command in result
                               where Command.FinishMethod.Equals(FinishMethod)
                               select Command;
                if (findPath.Count() != 0)
                {
                    result = findPath.ToList();
                }
                else
                {
                    result = new List<CommandScript>();
                }
            }
            return result;
        }

        public static List<CommandScript> GetExcuteNext(string ScriptName, string ExcuteMethod)
        {
            List<CommandScript> result = new List<CommandScript>();

            if (CommandScriptList.TryGetValue(ScriptName, out result))
            {


                var findPath = from Command in result
                               where Command.ExcuteMethod.Equals(ExcuteMethod)
                               select Command;
                if (findPath.Count() != 0)
                {
                    result = findPath.ToList();
                }
                else
                {
                    result = new List<CommandScript>();
                }
            }
            return result;
        }
    }
}
