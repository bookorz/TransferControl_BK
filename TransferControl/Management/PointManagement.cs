using log4net;
using Newtonsoft.Json;
using SANWA.Utility;
using SANWA.Utility.Config;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TransferControl.Management
{
    public class PointManagement
    {
        public static Dictionary<string, List<RobotPoint>> PointList;
        static ILog logger = LogManager.GetLogger(typeof(PointManagement));

        private static DBUtil dBUtil = new DBUtil();
        public static void LoadConfig()
        {
            PointList = new Dictionary<string, List<RobotPoint>>();
            Dictionary<string, object> keyValues = new Dictionary<string, object>();
            string Sql = @"SELECT t.recipe_id AS RecipeID,t.node_name AS NodeName,t.position AS POSITION,t.position_type AS PositionType,t.point as Point, t.`offset` as Offset 
                            FROM config_point t 
                            WHERE t.equipment_model_id = @equipment_model_id";
            keyValues.Add("@equipment_model_id", SystemConfig.Get().SystemMode);
            DataTable dt = dBUtil.GetDataTable(Sql, keyValues);
            string str_json = JsonConvert.SerializeObject(dt, Formatting.Indented);
            List<RobotPoint> cmdScpList = JsonConvert.DeserializeObject<List<RobotPoint>>(str_json);

            List<RobotPoint> tmp;
            foreach (RobotPoint each in cmdScpList)
            {
                if (PointList.TryGetValue(each.RecipeID, out tmp))
                {
                    tmp.Add(each);
                }
                else
                {
                    tmp = new List<RobotPoint>();
                    tmp.Add(each);
                    PointList.Add(each.RecipeID, tmp);
                }

            }
        }

        public static List<RobotPoint> GetPointList(string NodeName, string RecipeID = "")
        {
            List<RobotPoint> result = null;
            if (RecipeID.Equals(""))
            {
                RecipeID = PointList.Keys.First();
            }

            if (PointList.TryGetValue(RecipeID, out result))
            {
                var findPoint = from point in result
                                where point.NodeName.ToUpper().Equals(NodeName.ToUpper())
                                select point;
                if (findPoint.Count() != 0)
                {
                    result = findPoint.ToList();
                }

            }
            return result;
        }

        public static List<RobotPoint> GetPointList(string RecipeID = "")
        {
            List<RobotPoint> result = null;
            if (RecipeID.Equals(""))
            {
                RecipeID = PointList.Keys.First();
            }

            PointList.TryGetValue(RecipeID, out result);


            return result;
        }

        public static RobotPoint GetPoint(string NodeName, string Position, string RecipeID)
        {
            RobotPoint result = null;
            List<RobotPoint> tmp;           
            if (PointList.TryGetValue(RecipeID, out tmp))
            {
                var findPoint = from point in tmp
                                where point.NodeName.ToUpper().Equals(NodeName.ToUpper()) && point.Position.ToUpper().Equals(Position.ToUpper())
                                select point;
                if (findPoint.Count() != 0)
                {
                    result = findPoint.First();
                }

            }
            return result;
        }

        public static RobotPoint GetMapPoint(string Position, string RecipeID)
        {
            RobotPoint result = null;
            List<RobotPoint> tmp;
            if (PointList.TryGetValue(RecipeID, out tmp))
            {
                var findPoint = from point in tmp
                                where point.Position.ToUpper().Equals(Position.ToUpper()) && point.PositionType.Equals("MAPPER")
                                select point;
                if (findPoint.Count() != 0)
                {
                    result = findPoint.First();
                }

            }
            return result;
        }
    }
}
