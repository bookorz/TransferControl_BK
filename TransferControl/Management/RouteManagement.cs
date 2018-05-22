using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TransferControl.Management
{
    public static class RouteManagement
    {


        private static Dictionary<string, Dictionary<string, string>> RobotRoute = new Dictionary<string, Dictionary<string, string>>();

        public static void AddRoute(string NodeName ,Dictionary<string, string> route)
        {
            RobotRoute.Add(NodeName, route);
        }

        public static List<string> GetRouteList(string NodeName)
        {
            List<string> result = new List<string>();
            Dictionary<string, string> tmp;
            if(RobotRoute.TryGetValue(NodeName, out tmp))
            {
                result = tmp.Keys.ToList();
            }

            return result;
        }

        public static string GetPno(string NodeName,string Route)
        {
            string result = "";
            Dictionary<string, string> tmp;
            if (RobotRoute.TryGetValue(NodeName, out tmp))
            {
                tmp.TryGetValue(Route, out result);
            }
            return result;
        }

    }
}
