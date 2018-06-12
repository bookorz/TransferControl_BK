using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TransferControl.Management
{
    public class CmdParamManagement
    {
        public class ParamMapping
        {
            public string FuncType { get; set; }
            public string ParamName { get; set; }
            public string CodeId { get; set; }
            public string CodeDesc { get; set; }
            public string Vendor { get; set; }
            public string MappingCode { get; set; }
        }

        static Dictionary<string, ParamMapping> MappingList = new Dictionary<string, ParamMapping>();

        public static void Initialize()
        {
            try
            {
                DBUtil dBUtil = new DBUtil();

                DataTable dtCommand = new DataTable();

                string strSql = "SELECT  func_type,param_name, code_id, code_desc, vendor, mapping_code FROM param_mapping";



                dtCommand = dBUtil.GetDataTable(strSql, null);

                if (dtCommand.Rows.Count > 0)
                {
                    foreach (DataRow row in dtCommand.Rows)
                    {
                        ParamMapping each = new ParamMapping();
                        each.CodeDesc = row["code_desc"].ToString();
                        each.CodeId = row["code_id"].ToString();
                        each.FuncType = row["func_type"].ToString();
                        each.ParamName = row["param_name"].ToString();
                        each.MappingCode = row["mapping_code"].ToString();
                        each.Vendor = row["vendor"].ToString();
                        string key = each.Vendor + each.FuncType + each.ParamName + each.CodeId;
                        MappingList.Add(key, each);
                    }
                }
                else
                {
                    throw new Exception("TransferControl.Management.CmdParamManagement\r\nException: Parameter List not exists.");
                }
            }
            catch (Exception ex)
            {
                throw new Exception(ex.StackTrace);
            }

        }
        public static ParamMapping FindMapping(string Vendor,string FuncType, string ParamName,string CodeId)
        {
            ParamMapping result = null;
            string key = Vendor + FuncType + ParamName+ CodeId;

            MappingList.TryGetValue(key, out result);

            return result;
        }
    }
}
