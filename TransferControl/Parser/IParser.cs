using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TransferControl.Parser
{
    interface IParser
    {
        Dictionary<string, string> Parse(string Command, string Message);
        
    }
}
