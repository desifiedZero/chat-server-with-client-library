using System;

namespace ChatLibrary.Exceptions
{
    public class ConnectionNotPossibleException : Exception
    {
        public ConnectionNotPossibleException() : base() { }

        public ConnectionNotPossibleException(string? message) : base(message) { }

        public ConnectionNotPossibleException(string? message, Exception? innerException) : base(message, innerException) { }

    }
}
