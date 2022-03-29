using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatLibrary
{
    interface IMessage
    {
        /// <summary>
        /// Sender ClientID
        /// </summary>
        string SenderClientID { get; set; }
        /// <summary>
        /// Intended recipient
        /// </summary>
        string ReceiverClientID { get; set; }
        /// <summary>
        /// Message as sent by the client
        /// </summary>
        string MessageBody { get; set; }
        /// <summary>
        /// True if message is broadcast 
        /// </summary>
        bool Broadcast { get; set; }

        string ToString();
    }
}
