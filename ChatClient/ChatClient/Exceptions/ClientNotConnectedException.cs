using System;

namespace ChatLibrary.Exceptions
{
    public class ClientNotConnectedException : Exception
    {
        public ClientNotConnectedException() : base() { }

        public ClientNotConnectedException(string? message) : base(message) { }

        public ClientNotConnectedException(string? message, Exception? innerException) : base(message, innerException) { }

    }
}
