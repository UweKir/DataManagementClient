using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataManagement.DataClientLib
{
    public class Header
    {
        private String key = "UNKNOWN";
        private String unity = "UNKNOWN";
        private String article = "UNKNOWN";
        private String device = "UNKNOWN";

        public String Key { get { return key; } set { key = value; } }
        public String Unity { get { return unity; } set { unity = value; } }
        public String Article { get { return article; } set { article = value; } }
        public String Device { get { return device; } set { device = value; } }
        
    }
}
