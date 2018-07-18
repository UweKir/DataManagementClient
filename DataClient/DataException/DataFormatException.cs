using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataManagement.DataClientLib.DataException
{

    public class DataFormatException : Exception
    {
        public DataFormatException()
        { }

        public DataFormatException(string message)
            : base(message)
        { }

        public DataFormatException(string message, Exception inner)
            : base(message, inner)
        { }
    }
}
