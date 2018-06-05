using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TransferControl.Config;

namespace TransferControl.Management
{
    public class CommandScriptManagement
    {
        static Dictionary<string, List<CommandScript>> CommandScriptList = new Dictionary<string, List<CommandScript>>();

        public static void LoadConfig()
        {

            foreach (string FilePath in Directory.GetFiles("config/CommandScript"))
            {
                ConfigTool<CommandScript> DeviceCfg = new ConfigTool<CommandScript>();
                List<CommandScript> tmp = new List<CommandScript>();
                foreach (CommandScript each in DeviceCfg.ReadFileByList(FilePath))
                {

                    tmp.Add(each);
                }

                CommandScriptList.Add(System.IO.Path.GetFileNameWithoutExtension(FilePath), tmp);
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
