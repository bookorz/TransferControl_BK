﻿using log4net;
using SANWA.Utility;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TransferControl.Management
{
    public class TransactionRecord
    {

        static ILog logger = LogManager.GetLogger(typeof(TransactionRecord));
        public static string GetUUID()
        {
            string result = "";

            DBUtil dBUtil = new DBUtil();
            Dictionary<string, object> keyValues = new Dictionary<string, object>();
            DataTable dtTemp;

            try
            {
                string SQL = "select UUID() uuid from dual";
                dtTemp = dBUtil.GetDataTable(SQL, keyValues);
                if (dtTemp.Rows.Count > 0)
                {
                    result = dtTemp.Rows[0]["uuid"].ToString();
                }
                else
                {
                    logger.Error("GetUUID error: return 0 row.");
                }
            }
            catch (Exception e)
            {
                logger.Error("GetUUID error:" + e.StackTrace);
            }

            return result;
        }

        public static bool New(Transaction Txn, string txn_status = "Sent")
        {
            bool result = false;
            DBUtil dBUtil = new DBUtil();
            Dictionary<string, object> keyValues = new Dictionary<string, object>();

            try
            {
                DateTime SendTime = DateTime.Now;
                Txn.uuid = GetUUID();
                if (!Txn.uuid.Equals(""))
                {
                    string SQL = @"insert into log_cmd_txn  
                                    (txn_id,node_name,node_type,txn_status,txn_start_time,txn_method,txn_position,txn_slot,txn_arm,txn_value,script_name,form_name,return_value,cmd_type,time_stamp)
                                    values(@txn_id,@node_name,@node_type,@txn_status,@txn_start_time,@txn_method,@txn_position,@txn_slot,@txn_arm,@txn_value,@script_name,@form_name,'',@cmd_type,@time_stamp)";

                    keyValues.Add("@txn_id", Txn.uuid);
                    keyValues.Add("@node_name", Txn.NodeName);
                    keyValues.Add("@node_type", Txn.NodeType);
                    keyValues.Add("@txn_status", txn_status);
                    keyValues.Add("@txn_method", Txn.Method);
                    keyValues.Add("@txn_position", Txn.Position);
                    keyValues.Add("@txn_slot", Txn.Slot);
                    keyValues.Add("@txn_arm", Txn.Arm);
                    keyValues.Add("@txn_value", Txn.Value);
                    keyValues.Add("@script_name", Txn.ScriptName);
                    keyValues.Add("@form_name", Txn.FormName);
                    //keyValues.Add("@encodestr", Txn.CommandEncodeStr);
                    keyValues.Add("@cmd_type", Txn.CommandType);
                    //keyValues.Add("@txn_start_time", SendTime);
                    keyValues.Add("@txn_start_time", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.ffffff"));
                    keyValues.Add("@time_stamp", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.ffffff"));
                    dBUtil.ExecuteNonQueryAsync(SQL, keyValues);

                    AddDetail(Txn.uuid, Txn.NodeName, Txn.NodeType, txn_status, "", Txn.CommandEncodeStr, SendTime);

                    result = true;


                }
            }
            catch (Exception e)
            {
                logger.Error("New error:" + e.StackTrace);
            }
            return result;
        }

        public static bool Update(Transaction Txn, ReturnMessage Msg)
        {
            bool result = false;

            DBUtil dBUtil = new DBUtil();
            Dictionary<string, object> keyValues = new Dictionary<string, object>();
            DateTime ReceiveTime = DateTime.Now;
            try
            {
                if (!Txn.uuid.Equals(""))
                {
                    string SQL = @"update log_cmd_txn  set txn_end_time = @txn_end_time,txn_status = @txn_status,return_value = @return_value where txn_id = @txn_id";

                    keyValues.Add("@txn_id", Txn.uuid);
                    keyValues.Add("@txn_status", Msg.Type);
                    keyValues.Add("@return_value", Msg.Value);
                    keyValues.Add("@txn_end_time", ReceiveTime.ToString("yyyy-MM-dd HH:mm:ss.ffffff"));


                    dBUtil.ExecuteNonQueryAsync(SQL, keyValues);
                    AddDetail(Txn.uuid, Txn.NodeName, Txn.NodeType, Msg.Type, Msg.Value, Msg.OrgMsg, ReceiveTime);

                    result = true;


                }
            }
            catch (Exception e)
            {
                logger.Error("Update error:" + e.StackTrace);
            }

            return result;
        }

        public static bool AddDetail(string txn_id, string node_name, string node_type, string return_type, string return_value, string raw_data, DateTime receive_time)
        {
            bool result = false;

            DBUtil dBUtil = new DBUtil();
            Dictionary<string, object> keyValues = new Dictionary<string, object>();

            try
            {
                string SQL = @"insert into log_cmd_txn_detail (txn_id,return_type,receive_time,return_value,raw_data) 
                                values(@txn_id,@return_type,@receive_time,@return_value,@raw_data)";

                keyValues.Add("@txn_id", txn_id);
                keyValues.Add("@return_type", return_type);
                keyValues.Add("@return_value", return_value);
                keyValues.Add("@raw_data", raw_data);
                //keyValues.Add("@receive_time", receive_time);
                keyValues.Add("@receive_time", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.ffffff"));
                dBUtil.ExecuteNonQueryAsync(SQL, keyValues);

                result = true;


            }
            catch (Exception e)
            {
                logger.Error("AddDetail error:" + e.StackTrace);
            }

            return result;
        }
    }
}
