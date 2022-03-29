using System;

namespace ChatLibrary.Exceptions
{
    public class MessageNotSentException : Exception
    {
        public MessageNotSentException() : base() { }
        
        public MessageNotSentException(string? message) : base(message) { }

        public MessageNotSentException(string? message, Exception? innerException) : base(message, innerException) { }
    }
}
