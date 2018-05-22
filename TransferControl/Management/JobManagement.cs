using System;
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

        public static List<string> GetJobIDByPiority(string Position)
        {

            List<string> result = new List<string>();
            lock (JobList)
            {
                List<Job> SortByPiority = JobList.Values.ToList();
                SortByPiority.Sort((x, y) => { return -x.Piority.CompareTo(y.Piority); });

                foreach (Job eachJob in SortByPiority)
                {

                    if (eachJob.Position.Equals(Position) && !eachJob.Position.Equals(eachJob.Destination))
                    {
                        result.Add(eachJob.Job_Id);
                        break;
                    }

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
