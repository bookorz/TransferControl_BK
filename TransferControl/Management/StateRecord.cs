using log4net;
using SANWA.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TransferControl.Management
{
    public class StateRecord
    {
        static ILog logger = LogManager.GetLogger(typeof(StateRecord));
        public static bool NodeStateUpdate(string NodeName, string OldState, string NewState)
        {
            bool result = false;

            DBUtil dBUtil = new DBUtil();
            Dictionary<string, object> keyValues = new Dictionary<string, object>();
            DateTime UpdateTime = DateTime.Now;
            try
            {

                string SQL = @"replace into node_current_state (node_Name,node_state,update_time)
                                    values(@node_Name,@node_state,@update_time)";

                keyValues.Add("@node_Name", NodeName);
                keyValues.Add("@node_state", NewState);
                keyValues.Add("@update_time", UpdateTime);



                int ReturnVal = dBUtil.ExecuteNonQuery(SQL, keyValues);
                if (ReturnVal != 0)
                {
                    NodeStateAddHis(NodeName, OldState, NewState, UpdateTime);
                    result = true;
                }
                else
                {
                    logger.Error("Update error, no data update.");
                }

            }
            catch (Exception e)
            {
                logger.Error("Update error:" + e.StackTrace);
            }

            return result;
        }

        private static void NodeStateAddHis(string NodeName, string OldState, string NewState, DateTime UpdateTime)
        {
            DBUtil dBUtil = new DBUtil();
            Dictionary<string, object> keyValues = new Dictionary<string, object>();
            string SQL = @"insert into node_state_history (node_name,old_state,new_state,Update_time)
                            values(@node_name,@old_state,@new_state,@update_time)";

            keyValues.Add("@node_name", NodeName);
            keyValues.Add("@old_state", OldState);
            keyValues.Add("@new_state", NewState);
            keyValues.Add("@update_time", UpdateTime);



            int ReturnVal = dBUtil.ExecuteNonQuery(SQL, keyValues);
            if (ReturnVal == 0)
            {
                logger.Error("Update error, no data update.");
            }
        }

        public static bool EqpStateUpdate(string EqpName, string OldState, string NewState)
        {
            bool result = false;

            DBUtil dBUtil = new DBUtil();
            Dictionary<string, object> keyValues = new Dictionary<string, object>();
            DateTime UpdateTime = DateTime.Now;
            try
            {

                string SQL = @"replace into eqp_current_state (eqp_Name,eqp_state,update_time)
                                    values(@eqp_Name,@eqp_state,@update_time)";

                keyValues.Add("@eqp_Name", EqpName);
                keyValues.Add("@eqp_state", NewState);
                keyValues.Add("@update_time", UpdateTime);



                int ReturnVal = dBUtil.ExecuteNonQuery(SQL, keyValues);
                if (ReturnVal != 0)
                {
                    EqpStateAddHis(EqpName, OldState, NewState, UpdateTime);
                    result = true;
                }
                else
                {
                    logger.Error("Update error, no data update.");
                }

            }
            catch (Exception e)
            {
                logger.Error("Update error:" + e.StackTrace);
            }

            return result;
        }

        private static void EqpStateAddHis(string EqpName, string OldState, string NewState, DateTime UpdateTime)
        {
            DBUtil dBUtil = new DBUtil();
            Dictionary<string, object> keyValues = new Dictionary<string, object>();
            string SQL = @"insert into eqp_state_history (eqp_name,old_state,new_state,Update_time)
                            values(@eqp_name,@old_state,@new_state,@update_time)";

            keyValues.Add("@eqp_name", EqpName);
            keyValues.Add("@old_state", OldState);
            keyValues.Add("@new_state", NewState);
            keyValues.Add("@update_time", UpdateTime);



            int ReturnVal = dBUtil.ExecuteNonQuery(SQL, keyValues);
            if (ReturnVal == 0)
            {
                logger.Error("Update error, no data update.");
            }
        }
    }

}
