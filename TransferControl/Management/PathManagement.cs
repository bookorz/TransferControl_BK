using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TransferControl.Config;

namespace TransferControl.Management
{
    public class PathManagement
    {

        public static Dictionary<string, List<Path>> ScriptList = new Dictionary<string, List<Path>>();

        public static void LoadConfig()
        {

            foreach (string FilePath in Directory.GetFiles("config/Script"))
            {
                if (!System.IO.Path.GetExtension(FilePath).ToUpper().Equals(".JSON"))
                {
                    continue;
                }
                ConfigTool<Path> DeviceCfg = new ConfigTool<Path>();
                List<Path> tmp = new List<Path>();
                foreach (Path each in DeviceCfg.ReadFileByList(FilePath))
                {

                    tmp.Add(each);
                }

                ScriptList.Add(System.IO.Path.GetFileNameWithoutExtension(FilePath), tmp);
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
