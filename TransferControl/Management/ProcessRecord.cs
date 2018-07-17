using log4net;
using SANWA.Utility;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TransferControl.Management
{
    public class ProcessRecord
    {
        static ILog logger = LogManager.GetLogger(typeof(ProcessRecord));
        static DBUtil dBUtil = new DBUtil();
        public static string GetUUID()
        {
            string result = "";


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

        public static void CreatePr(Node Port)
        {

            Dictionary<string, object> keyValues = new Dictionary<string, object>();

            try
            {

                if (Port.PrID.Equals(""))
                {
                    Port.PrID = GetUUID();
                }
                string SQL = @"insert into log_process_job (pr_id,foup_id,slot_list,process_cnt,create_time,time_stamp)
                                    values(@pr_id,@foup_id,@slot_list,@process_cnt,@create_time,now())";

                keyValues.Add("@pr_id", Port.PrID);
                keyValues.Add("@foup_id", Port.FoupID);
                var findJob = from j in Port.JobList.Values.ToList()
                              where j.NeedProcess
                              select j;
                List<Job> tmp = findJob.ToList();
                tmp.Sort((x, y) => { return Convert.ToInt16(x.Slot).CompareTo(Convert.ToInt16(y.Slot)); });
                string SlotList = "";
                foreach (Job job in tmp)
                {
                   
                    if (!SlotList.Equals(""))
                    {
                        SlotList += ",";
                    }
                    SlotList += job.Slot;
                }
                keyValues.Add("@slot_list", SlotList);
                keyValues.Add("@process_cnt", findJob.Count().ToString());
                keyValues.Add("@create_time", DateTime.Now);


                int ReturnVal = dBUtil.ExecuteNonQuery(SQL, keyValues);
                if (ReturnVal == 0)
                {
                    logger.Error("NewPr error.");
                }
                else
                {
                    AddDetail(Port, "Create", "QUEUE");
                    AddSubstrate(Port);
                }

            }
            catch (Exception e)
            {
                logger.Error("NewPr error:" + e.StackTrace);
            }

        }

        public static void CancelPr(Node Port)
        {

            Dictionary<string, object> keyValues = new Dictionary<string, object>();

            try
            {
                AddDetail(Port, "Abort", "ABORTED");
                
                Port.PrID = "";
            }
            catch (Exception e)
            {
                logger.Error("NewPr error:" + e.StackTrace);
            }

        }

        public static void FinishPr(Node Port)
        {

            Dictionary<string, object> keyValues = new Dictionary<string, object>();

            try
            {
                AddDetail(Port, "Finish", "COMPLETE");
                
                Port.PrID = "";
            }
            catch (Exception e)
            {
                logger.Error("NewPr error:" + e.StackTrace);
            }

        }

        public static void AddDetail(Node Port, string event_type, string job_status)
        {
            Dictionary<string, object> keyValues = new Dictionary<string, object>();

            try
            {

                string SQL = @"insert into log_process_job_detail (pr_id,event_type,job_status,event_time)
                                values(@pr_id,@event_type,@job_status,@event_time)";

                keyValues.Add("@pr_id", Port.PrID);
                keyValues.Add("@event_type", event_type);
                keyValues.Add("@job_status", job_status);
                keyValues.Add("@event_time", DateTime.Now);

                int ReturnVal = dBUtil.ExecuteNonQuery(SQL, keyValues);
                if (ReturnVal == 0)
                {
                    logger.Error("AddDetail error.");
                }
                else
                {
                    
                    //if (!event_type.Equals("Start") && !event_type.Equals("Finish") && !event_type.Equals("Create"))
                    //{
                    //    foreach (Job j in Port.JobList.Values.ToList())
                    //    {

                    //        if (j.NeedProcess)
                    //        {
                    //            ProcessRecord.updateSubstrateStatus(Port.PrID, j, job_status);
                    //        }
                    //    }
                    //}
                }

            }
            catch (Exception e)
            {
                logger.Error("AddDetail error:" + e.StackTrace);
            }
        }

        public static void AddSubstrate(Node Port)
        {
            

            try
            {

                foreach (Job Job in Port.JobList.Values.ToList())
                {
                    if (!Job.NeedProcess)
                    {
                        continue;
                    }
                    Node FromPort = NodeManagement.Get(Job.FromPort);
                    Node ToPort = NodeManagement.Get(Job.Destination);
                    string SQL = @"insert into log_process_job_substrate
(pr_id,host_id,from_position,from_position_slot,to_position,to_position_slot,from_foup_id,to_foup_id,job_status,ocr_result,ocr_path,create_time)
values(@pr_id,@host_id,@from_position,@from_position_slot,@to_position,@to_position_slot,@from_foup_id,@to_foup_id,@job_status,@ocr_result,@ocr_path,@create_time)";
                    Dictionary<string, object> keyValues = new Dictionary<string, object>();
                    keyValues.Add("@pr_id", Port.PrID);
                    keyValues.Add("@host_id", Job.Host_Job_Id);
                    keyValues.Add("@from_position", Job.FromPort);
                    keyValues.Add("@from_position_slot", Job.FromPortSlot);
                    keyValues.Add("@to_position", Job.Destination);
                    keyValues.Add("@to_position_slot", Job.DestinationSlot);
                    keyValues.Add("@from_foup_id", FromPort.FoupID);
                    keyValues.Add("@to_foup_id", ToPort.FoupID);
                    keyValues.Add("@job_status", "QUEUED");
                    keyValues.Add("@ocr_result", "");
                    keyValues.Add("@ocr_path", "");
                    keyValues.Add("@create_time", DateTime.Now);


                    int ReturnVal = dBUtil.ExecuteNonQuery(SQL, keyValues);
                    if (ReturnVal == 0)
                    {
                        logger.Error("AddSubstrate error.");
                    }
                }

            }
            catch (Exception e)
            {
                logger.Error("AddSubstrate error:" + e.StackTrace);
            }
        }

        public static void updateSubstrateStatus(string pr_id, Job Job, string JobStatus)
        {
            Dictionary<string, object> keyValues = new Dictionary<string, object>();

            try
            {

                string SQL = @"update log_process_job_substrate t
                                set t.job_status = @job_status
                                where t.pr_id = @pr_id
                                and t.host_id = @host_id
                                and t.from_position_slot = @from_position_slot";

                keyValues.Add("@pr_id", pr_id);
                keyValues.Add("@host_id", Job.Host_Job_Id);
                keyValues.Add("@from_position_slot", Job.FromPortSlot);
                keyValues.Add("@job_status", JobStatus);

                int ReturnVal = dBUtil.ExecuteNonQuery(SQL, keyValues);
                if (ReturnVal == 0)
                {
                    logger.Error("updateSubstrateStatus error.");
                }

            }
            catch (Exception e)
            {
                logger.Error("updateSubstrateStatus error:" + e.StackTrace);
            }
        }

        public static void updateSubstrateOCR(string pr_id, Job Job)
        {
            Dictionary<string, object> keyValues = new Dictionary<string, object>();

            try
            {

                string SQL = @"update log_process_job_substrate t
                                set t.ocr_result = @ocr_result,t.ocr_path= @ ocr_path
                                where t.pr_id = @pr_id
                                and t.host_id = @host_id
                                and t.from_position_slot = @from_position_slot";

                keyValues.Add("@pr_id", pr_id);
                keyValues.Add("@host_id", Job.Host_Job_Id);
                keyValues.Add("@from_position_slot", Job.FromPortSlot);
                keyValues.Add("@ocr_result", Job.Job_Id);
                keyValues.Add("@ocr_path", Job.OCRImgPath);

                int ReturnVal = dBUtil.ExecuteNonQuery(SQL, keyValues);
                if (ReturnVal == 0)
                {
                    logger.Error("updateSubstrateStatus error.");
                }

            }
            catch (Exception e)
            {
                logger.Error("updateSubstrateStatus error:" + e.StackTrace);
            }
        }
    }
}
