using System;

namespace ChatLibrary
{
    [Serializable]
    public class Message : IMessage
    {
        public string SenderClientID { get; set; }
        public string ReceiverClientID { get; set; }
        public string MessageBody { get; set; }
        public bool Broadcast { get; set; }

        public Message(string senderClientID, string receiverClientID, string messageBody, bool broadcast)
        {
            SenderClientID = senderClientID ?? throw new ArgumentNullException(nameof(senderClientID));
            ReceiverClientID = receiverClientID;
            MessageBody = messageBody;
            Broadcast = broadcast;
        }

        public override string ToString()
        {
            return "SENDER: " + SenderClientID +
                "\nRECIEVER: " + ReceiverClientID +
                "\nBODY: " + MessageBody +
                "\nBROADCAST: " + Broadcast +
                "\r\n\r\n";
        }
    }
}
