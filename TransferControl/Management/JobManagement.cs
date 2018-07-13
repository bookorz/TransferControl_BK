﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TransferControl.Management
{
    public static class JobManagement
    {
        private static ConcurrentDictionary<string, Job> JobList = new ConcurrentDictionary<string, Job>();

        public static void Initial()
        {
            JobList.Clear();
        }

        public static void ClearAssignJobByPort(string PortName)
        {
            var findAssignJob = from job in GetJobList()
                                where job.Destination.Equals(PortName)
                                select job;

            if (findAssignJob.Count() != 0)
            {
                foreach (Job j in findAssignJob)
                {
                    j.UnAssignPort();
                }
            }
        }

        public static List<Job> GetJobList()
        {

            List<Job> result = new List<Job>();
            lock (JobList)
            {
                lock (JobList)
                {
                    result = JobList.Values.ToList();
                    result.Sort((x, y) => { return -x.Position.CompareTo(y.Position); });
                }
            }
            return result;
        }

        
        public static Job Get(string Job_Id)
        {
            Job result = null;

            lock (JobList)
            {
                JobList.TryGetValue(Job_Id, out result);
            }

            return result;
        }
        public static bool Add(string Job_Id, Job Job)
        {
            bool result = false;
            lock (JobList)
            {
                if (!JobList.ContainsKey(Job_Id))
                {
                    JobList.TryAdd(Job_Id, Job);
                    result = true;
                }
            }
            return result;
        }
        public static bool Remove(string Job_Id)
        {
            bool result = false;
            lock (JobList)
            {
                Job tmp;
                result = JobList.TryRemove(Job_Id ,out tmp);

            }
            return result;
        }
    }
}
