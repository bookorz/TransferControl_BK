using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TransferControl.Management;

namespace TransferControl.Parser
{
    class TDKParser : IParser
    {
        public Dictionary<string, string> Parse( string Command, string Message)
        {
            switch (Command)
            {
                case Transaction.Command.LoadPortType.ReadStatus:
                    return ParseStatus(Message);
                default:
                    throw new Exception(Command +" Not support");
            }
            
        }

        private Dictionary<string, string> ParseStatus(string Message)
        {
            Dictionary<string, string> result = new Dictionary<string, string>();

            for (int i = 0; i < 19; i++)
            {
                string Idx = (i + 1).ToString("00");
                string Sts = "";
                switch (Idx)
                {
                    case "01":
                        switch (Message[i])
                        {
                            case '0':
                                Sts = "Normal";
                                break;
                            case 'A':
                                Sts = "Recoverable error";
                                break;
                            case 'E':
                                Sts = "Fatal error";
                                break;
                        }
                        result.Add("Equipment Status", Sts);
                        break;
                    case "02":
                        switch (Message[i])
                        {
                            case '0':
                                Sts = "Online";
                                break;
                            case '1':
                                Sts = "Teaching";
                                break;
                        }
                        result.Add("Mode", Sts);
                        break;
                    case "03":
                        switch (Message[i])
                        {
                            case '0':
                                Sts = "Unexecuted";
                                break;
                            case '1':
                                Sts = "Executed";
                                break;
                        }
                        result.Add("Initial Position", Sts);
                        break;
                    case "04":
                        switch (Message[i])
                        {
                            case '0':
                                Sts = "Stopped";
                                break;
                            case '1':
                                Sts = "Operating";
                                break;
                        }
                        result.Add("Operation Status", Sts);
                        break;
                    case "05":
                    case "06":
                        result.TryGetValue("Error Code", out Sts);
                        if (Sts == null)
                        {
                            Sts = "";
                        }
                        Sts += Message[i].ToString();
                        result.Remove("Error Code");
                        result.Add("Error Code", Sts);

                        break;
                    case "07":
                        switch (Message[i])
                        {
                            case '0':
                                Sts = "None";
                                break;
                            case '1':
                                Sts = "Normal position";
                                break;
                            case '2':
                                Sts = "Error load";
                                break;
                        }
                        result.Add("Cassette Presence", Sts);
                        break;
                    case "08":
                        switch (Message[i])
                        {
                            case '0':
                                Sts = "Open";
                                break;
                            case '1':
                                Sts = "Close";
                                break;
                            case '?':
                                Sts = "Not defined";
                                break;
                        }
                        result.Add("FOUP Clamp Status", Sts);
                        break;
                    case "09":
                        switch (Message[i])
                        {
                            case '0':
                                Sts = "Open";
                                break;
                            case '1':
                                Sts = "Close";
                                break;
                            case '?':
                                Sts = "Not defined";
                                break;
                        }
                        result.Add("Latch Key Status", Sts);
                        break;
                    case "10":
                        switch (Message[i])
                        {
                            case '0':
                                Sts = "OFF";
                                break;
                            case '1':
                                Sts = "ON";
                                break;
                        }
                        result.Add("Vacumm", Sts);
                        break;
                    case "11":
                        switch (Message[i])
                        {
                            case '0':
                                Sts = "Open position";
                                break;
                            case '1':
                                Sts = "Close position";
                                break;
                            case '?':
                                Sts = "Not defined";
                                break;
                        }
                        result.Add("Door Position", Sts);
                        break;
                    case "12":
                        switch (Message[i])
                        {
                            case '0':
                                Sts = "Blocked.";
                                break;
                            case '1':
                                Sts = "Unblocked.";
                                break;
                        }
                        result.Add("Wafer Protrusion Sensor", Sts);
                        break;
                    case "13":
                        switch (Message[i])
                        {
                            case '0':
                                Sts = "Up position";
                                break;
                            case '1':
                                Sts = "Down position";
                                break;
                            case '2':
                                Sts = "Start position";
                                break;
                            case '3':
                                Sts = "End position";
                                break;
                            case '?':
                                Sts = "Not defined";
                                break;
                        }
                        result.Add("Z Axis Position", Sts);
                        break;
                    case "14":
                        switch (Message[i])
                        {
                            case '0':
                                Sts = "Undock position";
                                break;
                            case '1':
                                Sts = "Dock position";
                                break;
                            case '?':
                                Sts = "Not defined";
                                break;
                        }
                        result.Add("Y Axis Position", Sts);
                        break;
                    case "15":
                        switch (Message[i])
                        {
                            case '0':
                                Sts = "Open";
                                break;
                            case '1':
                                Sts = "Close";
                                break;
                            case '?':
                                Sts = "Not defined";
                                break;
                        }
                        result.Add("Mapper Arm Position", Sts);
                        break;
                    case "16":
                        switch (Message[i])
                        {
                            case '0':
                                Sts = "Retract position";
                                break;
                            case '1':
                                Sts = "Mapping position";
                                break;
                            case '?':
                                Sts = "Not defined";
                                break;
                        }
                        result.Add("Mapper Z Axis", Sts);
                        break;
                    case "17":
                        switch (Message[i])
                        {
                            case '0':
                                Sts = "ON";
                                break;
                            case '1':
                                Sts = "OFF";
                                break;
                            case '?':
                                Sts = "Not defined";
                                break;
                        }
                        result.Add("Mapper Stopper", Sts);
                        break;
                    case "18":
                        switch (Message[i])
                        {
                            case '0':
                                Sts = "Unexecuted";
                                break;
                            case '1':
                                Sts = "Normal end";
                                break;
                            case '2':
                                Sts = "Abnormal end";
                                break;
                        }
                        result.Add("Mapping Status", Sts);
                        break;
                    case "19":
                        switch (Message[i])
                        {
                            case '0':
                                Sts = "Enable";
                                break;
                            case '1':
                            case '2':
                            case '3':
                                Sts = "Disable";
                                break;
                        }
                        result.Add("Interlock Key", Sts);
                        break;
                    case "20":
                        switch (Message[i])
                        {
                            case '0':
                                Sts = "No input";
                                break;
                            case '1':
                                Sts = "A-pin ON";
                                break;
                            case '2':
                                Sts = "B-pin ON";
                                break;
                            case '3':
                                Sts = "A-pin/B-pin ON";
                                break;
                        }
                        result.Add("Info Pad", Sts);
                        break;
                }

                
            }
            return result;
        }
    }
}
