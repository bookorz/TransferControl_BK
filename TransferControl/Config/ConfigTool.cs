using log4net;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TransferControl.Config
{
    class ConfigTool<T>
    {
        ILog logger = LogManager.GetLogger(typeof(ConfigTool<T>));
        public List<T> ReadFileByList(string FilePath)
        {
            List<T> result = null;
            try
            {
                string t = File.ReadAllText(FilePath, Encoding.UTF8);
                result = JsonConvert.DeserializeObject<List<T>>(File.ReadAllText(FilePath, Encoding.UTF8));
            }
            catch (Exception ex)
            {
                logger.Error("ReadFileByList:" + ex.Message + "\n" + ex.StackTrace);
            }

            return result;
        }

        public T ReadFile(string FilePath)
        {
            
            try
            {
                string t = File.ReadAllText(FilePath, Encoding.UTF8);
                return JsonConvert.DeserializeObject<T>(File.ReadAllText(FilePath, Encoding.UTF8));
            }
            catch (Exception ex)
            {
                logger.Error("ReadFile:" + ex.Message + "\n" + ex.StackTrace);
            }

            return default(T);
        }

        public void WriteFileByList(string FilePath, List<T> Obj)
        {
            try
            {
                File.WriteAllText(FilePath, JsonConvert.SerializeObject(Obj));
            }
            catch (Exception ex)
            {
                logger.Error("WriteFileByList:" + ex.Message + "\n" + ex.StackTrace);
            }
        }

        public void WriteFile(string FilePath, T Obj)
        {
            try
            {
                File.WriteAllText(FilePath, JsonConvert.SerializeObject(Obj));
            }
            catch (Exception ex)
            {
                logger.Error("WriteFile:" + ex.Message + "\n" + ex.StackTrace);
            }
        }
    }
}
