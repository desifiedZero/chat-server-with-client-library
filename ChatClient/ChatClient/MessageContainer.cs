using System;

namespace ChatLibrary
{
    [Serializable]
    public class MessageContainer<T> : IMessageContainer
    {
        public string Type { get; set; }
        public T Payload { get; set; }

        public MessageContainer(string type, T payload)
        {
            Type = type;
            Payload = payload;
        }
    }
}
