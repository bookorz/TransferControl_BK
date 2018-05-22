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
                ConfigTool<Path> DeviceCfg = new ConfigTool<Path>();
                List<Path> tmp = new List<Path>();
                foreach (Path each in DeviceCfg.ReadFileByList(FilePath))
                {
                    
                    tmp.Add(each);
                }

                ScriptList.Add(System.IO.Path.GetFileNameWithoutExtension(FilePath), tmp);
            }
        }
        public static List<Path> GetPath(string ScriptName,string JobStatus, string FinishMethod)
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
          result = new List<Path>();
        }
        }
     

      
            return result;
        }

        public static List<Path> GetPath(string ScriptName, string ExcuteMethod)
        {
            List<Path> result = new List<Path>();
            if (ScriptList.TryGetValue(ScriptName, out result))
            {
                var find = from path in result
                           where path.JobStatus.Equals("") && path.ExcuteMethod.Equals(ExcuteMethod)
                           select path;
                if (find.Count() != 0)
                {
          result = find.ToList();
        }
        else
        {
          result = new List<Path>();
        }
      }
            return result;
        }
    }
}
