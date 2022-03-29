using System;

namespace ChatLibrary.Events
{
    public class MessageEventArgs<T> : EventArgs
    {
        public string Type { get; private set; }
        public T Payload { get; private set; }

        public MessageEventArgs(string type, T payload)
        {
            Type = type;
            Payload = payload;
        }
    }
}
