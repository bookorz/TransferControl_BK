using TransferControl.Controller;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TransferControl.Management
{
    public static class ControllerManagement
    {
        private static ConcurrentDictionary<string, DeviceController> Controllers = new ConcurrentDictionary<string, DeviceController>();

        public static DeviceController Get(string Name)
        {
            DeviceController result = null;

            Controllers.TryGetValue(Name, out result);

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
                each.Connect();
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
    }
}
