using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TransferControl.Management
{
    public class RobotPoint
    {
        public string RecipeID { get; set; }
        public string NodeName { get; set; }
        public string Position { get; set; }
        public string PositionType { get; set; }
        public string Point { get; set; }
        public int Offset { get; set; }
    }
}
