using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatLibrary.Events
{
    public class InternalExceptionEventArgs : EventArgs
    {
        public string ThreadGuid { get; private set; }
        public Exception ThrownException { get; private set; }

        public InternalExceptionEventArgs(string threadGuid, Exception thrownException)
        {
            ThreadGuid = threadGuid;
            ThrownException = thrownException;
        }
    }
}
