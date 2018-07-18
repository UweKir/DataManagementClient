using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataManagement.DataClientLib.DataException
{
    public class RemoteDBException: Exception
    {
        public RemoteDBException()
        { }

        public RemoteDBException(string message)
            : base(message)
        { }

        public RemoteDBException(string message, Exception inner)
            : base(message, inner)
        { }
    }
}
