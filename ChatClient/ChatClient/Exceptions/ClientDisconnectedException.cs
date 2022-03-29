using System;

namespace ChatLibrary.Exceptions
{
    public class ClientDisconnectedException : Exception
    {
        public ClientDisconnectedException() : base() { }
        
        public ClientDisconnectedException(string? message) : base(message) { }

        public ClientDisconnectedException(string? message, Exception? innerException) : base(message, innerException) { }

    }
}
