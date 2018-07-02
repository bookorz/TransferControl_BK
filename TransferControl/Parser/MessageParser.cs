using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TransferControl.Parser
{
    public class MessageParser
    {
        IParser P;
        public MessageParser(string Supplier)
        {
            switch (Supplier.ToUpper())
            {
                case "TDK":
                    P = new TDKParser();
                    break;
                case "SANWA":
                    P = new SanwaParser();
                    break;
                default:
                    throw new Exception(Supplier + " 不存在");
                    
            }
        }

        public Dictionary<string,string> ParseMessage(string Command, string Message)
        {
            return P.Parse(Command, Message);
        }
    }
}
