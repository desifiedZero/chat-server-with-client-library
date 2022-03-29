using ChatLibrary.Events;
using ChatLibrary.Exceptions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Permissions;
using System.Threading;

namespace ChatLibrary
{
    public class ChatClient : IDisposable
    {
        static readonly object _lockNetworkStream = new();

        public string ClientId { get; private set; }
        private TcpClient ClientSocket { get; set; }
        private NetworkStream TcpNetworkStream { get; set; }
        private Thread ListeningThread { get; set; }
        private CancellationTokenSource TokenSource { get; set; }
        public bool Connected { get; private set; }
        public List<string> Clients { get; private set; }

        public ChatClient() {
            TokenSource = new();
        }

        public void Init(string ip, int port)
        {
            if (ip == null || ip == "")
                throw new ArgumentException("IP address is empty");

            if (port < 0 || port > 65535)
            {
                throw new ArgumentException("Port out of range.", new ArgumentOutOfRangeException("Port out fo range. Range is between 0 and 65535."));
            }

            IPAddress clientIP;
            try
            {
                clientIP = IPAddress.Parse(ip);
            }
            catch (Exception)
            {
                throw;
            }

            ClientSocket = new();
            try
            {
                ClientSocket.Connect(clientIP, port);
                TcpNetworkStream = ClientSocket.GetStream();
            }
            catch (Exception)
            {
                throw new ConnectionNotPossibleException("Could not connect to server.");
            }

            byte[] dataLen = new byte[4];
            byte[] data;
            try
            {
                TcpNetworkStream.Read(dataLen, 0, 4);
                int dataLenInt = BitConverter.ToInt32(dataLen);
                data = new byte[dataLenInt];
                TcpNetworkStream.Read(data, 0, dataLenInt);
            }
            catch (Exception exception)
            {
                throw new ClientDisconnectedException("Could not establish connection to server.", exception);
            }

            if(data == null)
            {
                throw new NullReferenceException("Could not establish connection to server.");
            }

            using (MemoryStream memoryStream = new(data))
            {
                BinaryFormatter formatter = new();
                InitialMessage initialMessage = (InitialMessage)formatter.Deserialize(memoryStream);

                ClientId = initialMessage.Guid;
                Clients = initialMessage.Clients;
            }

            //
            //  Start listening on ClientSocket's TcpNetworkStream 
            //
            StartListening();
        }

        private void StartListening()
        {
            if(Connected)
            {
                return;
            }

            ListeningThread = new Thread(() =>
            {
                try
                {
                    Listen(TokenSource.Token);
                }
                catch (Exception)
                {
                    //
                    //  TODO: 
                    //      HANDLE THREAD EXCEPTIONS INTERNALLY
                    //
                }
            });

            try
            {
                ListeningThread.Start();
                Connected = true;
            }
            catch (Exception)
            {
                Close();
            }
        }

        public void Close()
        {
            Connected = false;
            TokenSource.Cancel();

            // EATING UP EXCEPTIONS
            try
            {
                // if throws exception, is not connected.
                ClientSocket.Client.Disconnect(false);      
            }
            catch (Exception) { }

            Dispose();
        }

        public void Dispose()
        {
            ClientSocket.Dispose();
        }

        private bool Send(string receiverId, string message, bool broadcast)
        {
            MessageContainer<Message> payload = new(
                "Message",
                new(ClientId, receiverId, message, broadcast)
                );

            byte[] data;
            byte[] dataLen;
            using (MemoryStream memoryStream = new())
            {
                BinaryFormatter formatter = new();
                formatter.Serialize(memoryStream, payload);
                data = memoryStream.ToArray();
                dataLen = BitConverter.GetBytes(data.Length);
            }

            try
            {
                lock (_lockNetworkStream)
                {
                    TcpNetworkStream.Write(dataLen, 0, 4);
                    TcpNetworkStream.Write(data, 0, data.Length);
                }
            }
            catch (IOException exception)
            {
                throw new ClientDisconnectedException("Message not sent: Target Client Disconnected.", exception);
            }
            catch (ObjectDisposedException exception)
            {
                throw new ClientDisconnectedException("Message not sent: Target Client Disconnected.", exception);
            }
            catch (Exception)
            {
                throw;
            }

            return true;
        }

        public bool SendUnicast(string receiverId, string message)
        {
            return Send(receiverId, message, false);
        }

        public bool SendBroadcast(string message)
        {
            return Send(null, message, true);
        }

        private void Listen(CancellationToken cancellationToken)
        {
            BinaryFormatter formatter = new BinaryFormatter();
            byte[] dataLen = new byte[4];
            byte[] data;

            while (!cancellationToken.IsCancellationRequested)
            {
                //  TODO:
                //      DO BETTER EXCEPTION HANDLING
                try
                {
                    TcpNetworkStream.Read(dataLen, 0, 4);
                    int dataLenInt = BitConverter.ToInt32(dataLen);
                    data = new byte[dataLenInt];
                    TcpNetworkStream.Read(data, 0, dataLenInt);
                }
                catch (ObjectDisposedException exception)
                {
                    throw new ClientDisconnectedException("Could not establish connection to server.", exception);
                }
                catch (IOException exception)
                {
                    throw new ClientDisconnectedException("Could not establish connection to server.", exception);
                }
                catch (Exception exception)
                {
                    throw new ArgumentException("Could not establish connection to server.", exception);
                }

                if (data == null)
                {
                    throw new NullReferenceException("Could not establish connection to server.");
                }

                using (MemoryStream memoryStream = new(data))
                {
                    IMessageContainer payloadObject = (IMessageContainer)formatter.Deserialize(memoryStream);

                    switch (payloadObject.Type)
                    {
                        case "Message":
                            {
                                MessageContainer<Message> payload = (MessageContainer<Message>)payloadObject;
                                Message message = payload.Payload;

                                MessageEventArgs<Message> args = new(payload.Type, message);
                                OnMessageRecieved(args);
                            }
                            break;

                        case "ListUpdated":
                            {
                                MessageContainer<List<string>> payload = (MessageContainer<List<string>>)payloadObject;
                                Clients = payload.Payload;

                                // BELOW IS PROBABLY ONLY TO DISPLAY ON CHANGE
                                MessageEventArgs<List<string>> args = new("ListUpdated", Clients);
                                OnClientListUpdated(args);
                            }
                            break;

                        case "Exception":
                            {
                                MessageContainer<Exception> payload = (MessageContainer<Exception>)payloadObject;
                                Exception exception = payload.Payload;

                                MessageEventArgs<Exception> args = new("Exception", exception);
                                OnServerExceptionThrown(args);
                            }
                            break;

                        default:
                            break;
                    }
                }

                 
            }
        }

        public override string ToString()
        {
            if(ListeningThread != null && ClientId != null)
            {
                return string.Format(
                               "CLIENT ID: {0}\r\nLISTENING THREAD ID: {1}\r\n---------------------------",
                               ClientId,
                               ListeningThread.ManagedThreadId
                               );
            }

            return "";
        }

        //
        //  EVENT HANDLING
        //      below are all the event handlers and event invocation methods
        //

        protected virtual void OnMessageRecieved(MessageEventArgs<Message> args)
        {
            MessageRecieved?.Invoke(this, args);
        }

        protected virtual void OnClientListUpdated(MessageEventArgs<List<string>> args)
        {
            ClientListUpdated?.Invoke(this, args);
        }

        protected virtual void OnServerExceptionThrown(MessageEventArgs<Exception> args)
        {
            ServerExceptionThrown?.Invoke(this, args);
        }

        // adding to delegate will be done in the console application

        public event EventHandler<MessageEventArgs<Message>> MessageRecieved;
        public event EventHandler<MessageEventArgs<List<string>>> ClientListUpdated;
        public event EventHandler<MessageEventArgs<Exception>> ServerExceptionThrown;
    }

}
