
using log4net;
using Newtonsoft.Json;
using SANWA.Utility;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TransferControl.Management
{
    public static class NodeManagement
    {
        private static ConcurrentDictionary<string, Node> NodeList = new ConcurrentDictionary<string, Node>();
        private static ConcurrentDictionary<string, Node> NodeListByCtrl = new ConcurrentDictionary<string, Node>();
        static ILog logger = LogManager.GetLogger(typeof(NodeManagement));

        private static DBUtil dBUtil = new DBUtil();

        public static void LoadConfig()
        {
            string Sql = @"SELECT t.node_id AS name,t.controller_id AS controller,t.conn_address AS adrno, t.node_type AS TYPE,t.vendor AS brand,t.bypass,t.enable_flg AS Enable,t.default_aligner AS defaultaligner,t.alternative_aligner AS alternativealigner,t.route_table AS routetable
                                FROM config_node t";
            DataTable dt = dBUtil.GetDataTable(Sql, null);
            string str_json = JsonConvert.SerializeObject(dt, Formatting.Indented);
            str_json = str_json.Replace("\"[", "[").Replace("]\"", "]").Replace("\\\"", "\"");
            List<Node> nodeList = JsonConvert.DeserializeObject<List<Node>>(str_json);
           
            foreach (Node each in nodeList)
            {
                if (each.Enable)
                {
                    each.InitialObject();
                    NodeList.TryAdd(each.Name, each);
                    NodeListByCtrl.TryAdd(each.Controller + each.AdrNo, each);
                }
            }
        }

        public static void InitialNodes()
        {
            foreach (Node each in NodeList.Values.ToList())
            {
                each.CurrentLoadPort = "";
                each.CurrentPosition = "";
                
                //each.InitialComplete = false;
                each.JobList.Clear();
               
                each.Phase = "";
                each.PutOut = false;
                //each.TransferQueue.Clear();

            }
        }

        public static bool IsNeedInitial()
        {
            bool result = false;
            var findNotInit = from node in NodeList.Values.ToList()
                              where !node.InitialComplete && !node.Type.Equals("OCR") && !node.Type.Equals("SYSTEM") && !node.ByPass
                              select node;
            if (findNotInit.Count() == 0)
            {
                result = false;
            }
            else
            {
                result = true;
            }
            return result;
        }

        public static string GetCurrentState()
        {
            string result = "";
            var findAlarm = from node in NodeList.Values.ToList()
                            where node.State.Equals("Alarm") && !node.ByPass
                            select node;
            if (findAlarm.Count() != 0)
            {
                result = "Alarm";
            }
            else
            {
                var findPause = from node in NodeList.Values.ToList()
                              where node.State.Equals("Pause") && !node.ByPass
                                select node;
                if (findPause.Count() != 0)
                {
                    result = "Pause";
                }
                else
                {
                    var findRun = from node in NodeList.Values.ToList()
                                  where node.State.Equals("Run") && !node.ByPass
                                  select node;
                    if (findRun.Count() != 0)
                    {
                        result = "Run";
                    }
                    else
                    {
                        var findIdle = from node in NodeList.Values.ToList()
                                       where node.State.Equals("Idle") && !node.ByPass
                                       select node;
                        if (findIdle.Count() != 0)
                        {
                            result = "Idle";
                        }
                        else
                        {
                            var findDown = from node in NodeList.Values.ToList()
                                           where node.State.Equals("Down") && !node.ByPass
                                           select node;
                            if (findDown.Count() != 0)
                            {
                                result = "Down";
                            }
                        }
                    }
                }
            }
            return result;
        }

        public static List<Node> GetLoadPortList()
        {
            List<Node> result = new List<Node>();

            var findPort = from port in NodeList.Values.ToList()
                           where port.Type.Equals("LoadPort")
                           select port;

            if (findPort.Count() != 0)
            {
                result = findPort.ToList();
                result.Sort((x, y) => { return x.Name.CompareTo(y.Name); });
            }

            return result;
        }

        public static List<Node> GetLoadPortList(string Mode)
        {
            List<Node> result = new List<Node>();

            var findPort = from port in NodeList.Values.ToList()
                           where port.Type.Equals("LoadPort") && port.Mode.Equals(Mode)
                           select port;

            if (findPort.Count() != 0)
            {
                result = findPort.ToList();
                result.Sort((x, y) => { return x.Name.CompareTo(y.Name); });
            }

            return result;
        }

        public static List<Node> GetEnableRobotList()
        {
            List<Node> result = new List<Node>();

            var findRobot = from robot in NodeList.Values.ToList()
                            where robot.Type.Equals("Robot") && robot.Enable == true
                            select robot;

            if (findRobot.Count() != 0)
            {
                result = findRobot.ToList();
            }

            return result;
        }

        public static List<Node> GetList()
        {
            List<Node> result = NodeList.Values.ToList();

            result.Sort((x, y) => { return y.Name.CompareTo(x.Name); });

            return result;
        }

        public static Node Get(string Name)
        {
            Node result = null;

            NodeList.TryGetValue(Name, out result);

            return result;
        }

        public static Node GetRobotByPosition(string Position, string filtName)
        {
            Node result = null;

            foreach (Node each in NodeList.Values.ToList())
            {

                if (each.CurrentPosition.Equals(Position) && !each.Name.Equals(filtName) && each.Type.Equals("Robot"))
                {
                    result = each;
                }
            }

            return result;
        }

        public static Node GetByController(string DeviceName, string NodeAdr)
        {
            Node result = null;

            NodeListByCtrl.TryGetValue(DeviceName + NodeAdr, out result);

            return result;
        }

        public static Node GetNextRobot(Node ProcessNode, Job Job)
        {
            Node result = null;

            foreach (Node.Route eachRt in ProcessNode.RouteTable)
            {
                Node tmp;
                if (eachRt.NodeType.Equals("Robot"))
                {
                    if (NodeList.TryGetValue(eachRt.NodeName, out tmp))
                    {
                        foreach (Node.Route eachtmpRt in tmp.RouteTable)
                        {
                            if (Job.Destination.Equals(eachtmpRt.NodeName))//尋找能搬送到目的地的Robot
                            {
                                result = tmp;
                            }
                        }
                    }
                }
            }

            return result;
        }

        public static Node GetNextRobot(string Destination)
        {
            Node result = null;

            Node Dest = Get(Destination);
            if (Dest == null)
            {
                return null;
            }

            foreach (Node.Route eachRt in Dest.RouteTable)
            {

                if (eachRt.NodeType.Equals("Robot"))
                {
                    if (NodeList.TryGetValue(eachRt.NodeName, out result))
                    {
                        break;
                    }
                }
            }

            return result;
        }

        public static Node GetOCRByAligner(Node Aligner)
        {
            Node result = null;

            foreach (Node.Route eachRt in Aligner.RouteTable)
            {
                if (eachRt.NodeType.Equals("OCR"))
                {
                    if (NodeList.TryGetValue(eachRt.NodeName, out result))
                    {
                        break;
                    }
                }
            }
            return result;
        }

        public static Node GetAlignerByOCR(Node OCR)
        {
            Node result = null;

            foreach (Node.Route eachRt in OCR.RouteTable)
            {
                if (eachRt.NodeType.Equals("Aligner"))
                {
                    if (NodeList.TryGetValue(eachRt.NodeName, out result))
                    {
                        break;
                    }
                }
            }
            return result;
        }

        public static bool Add(string Name, Node Node)
        {
            bool result = false;


            if (!NodeList.ContainsKey(Name))
            {
                NodeList.TryAdd(Name, Node);
                NodeListByCtrl.TryAdd(Node.Controller + Node.AdrNo, Node);
                result = true;
            }



            return result;
        }

    }
}
