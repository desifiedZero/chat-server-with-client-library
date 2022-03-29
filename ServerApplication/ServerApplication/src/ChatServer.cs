using ChatLibrary;
using ChatLibrary.Events;
using ChatLibrary.Exceptions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;

namespace ServerApplication
{
    class ChatServer
    {
        private static object _lockClientsCollection = new();
        private static object _lockTcpNetworkStream = new();
        private static object _lockRemovalQueue = new();

        private TcpListener TcpServerListener { get; set; }
        private Queue<string> RemoveQueue { get; set; }
        private List<string> RemovedClients { get; set; }
        private bool Scanning { get; set; }
        private Dictionary<string, TcpClient> Clients { get; set; }

        public ChatServer()
        {
            Clients = new();
            RemoveQueue = new();
            RemovedClients = new();
        }

        public void Init(IPAddress ip, int port)
        {
            if (port < 0 || port > 65535)
            {
                ArgumentOutOfRangeException innerException = new("Port out of range. Range is between 0 and 65535.");

                throw new ArgumentException("Port out of range.", innerException);
            }

            TcpServerListener = new(ip, port);

            try
            {
                TcpServerListener.Start();
            }
            catch (Exception exception)
            {
                Thread.Sleep(500);
                TcpServerListener.Start();
                throw new ConnectionNotPossibleException("Server Could not be started. Possibly invalid configuration.\r\nERROR: {0}", exception);
            }

            Scanning = true;

            Thread cleanupThread = new(() => RemoveUnused());
            cleanupThread.Start();

            FindClients();
        }

        private void RemoveUnused()
        {
            while (true)
            {
                if(RemoveQueue.Count > 0)
                {
                    string client;
                    lock (_lockRemovalQueue)
                    {
                        client = RemoveQueue.Dequeue();
                    }

                    if (client != null)
                    {
                        RemovedClients.Add(client);
                        RemoveClient(client);
                    }
                    
                    BroadcastUpdatedClientLists();
                }
            }
        }

        public void Close()
        {
            // Swallow the except 
            // if throws except, listener already closed
            try
            {
                TcpServerListener.Stop();
            }
            catch (Exception) { }

            Scanning = false;
        }

        private void FindClients()
        {
            while (Scanning)
            {
                TcpClient client = TcpServerListener.AcceptTcpClient();

                new Thread(() =>
                {
                    NetworkStream networkStream;
                    Guid guid;

                    try
                    {
                        networkStream = client.GetStream();
                    }
                    catch (Exception exception)
                    {
                        OnClientDisconnectEvent(new InternalExceptionEventArgs(
                            null,
                            new ConnectionNotPossibleException("Could not Connect to client.", exception)
                            ));
                        return;
                    }

                    guid = Guid.NewGuid();
                    string guidStr = guid.ToString();

                    InitialMessage message = new(Clients.Keys.ToList(), guidStr);

                    byte[] data;
                    byte[] dataLen;
                    using (MemoryStream memoryStream = new())
                    {
                        BinaryFormatter formatter = new();
                        formatter.Serialize(memoryStream, message);
                        data = memoryStream.ToArray();
                        dataLen = BitConverter.GetBytes(data.Length);
                    }

                    try
                    {
                        lock (_lockTcpNetworkStream)
                        {
                            networkStream.Write(dataLen, 0, 4);
                            networkStream.Write(data, 0, data.Length);
                        }

                        lock (_lockClientsCollection)
                        {
                            Clients.TryAdd(guidStr, client);
                        }

                        BroadcastUpdatedClientLists();
                    }
                    catch (IOException exception)
                    {
                        return;
                    }
                    catch (Exception exception)
                    {
                        OnClientDisconnectEvent(new InternalExceptionEventArgs(
                            guidStr,
                            new ConnectionNotPossibleException("3 Could not connect to client. Possible disconnection.", exception)
                            ));
                        return;
                    }

                    try
                    {
                        HandleClient(client, guidStr);
                    }
                    catch (ClientDisconnectedException exception)
                    {
                        //OnClientDisconnectEvent(new InternalExceptionEventArgs(
                        //    guidStr,
                        //    exception
                        //    ));
                    }
                    catch (Exception exception)
                    {
                        OnThrowExceptionEvent(new InternalExceptionEventArgs(
                            guidStr,
                            exception
                            ));
                    }
                }).Start();
            }
        }

        private void HandleClient(TcpClient client, string clientGuid)
        {
            byte[] dataLen = new byte[4];
            BinaryFormatter formatter = new();
            NetworkStream stream = client.GetStream();

            while (true)
            {
                int dataLenInt;
                byte[] data;
                try
                {
                    stream.Read(dataLen, 0, 4);
                    dataLenInt = BitConverter.ToInt32(dataLen);
                    data = new byte[dataLenInt];
                    stream.Read(data, 0, dataLenInt);
                }
                catch (ArgumentOutOfRangeException exception)
                {
                    stream.Flush();
                    OnThrowExceptionEvent(new InternalExceptionEventArgs(
                        clientGuid, new ClientDisconnectedException(string.Format("7 Could not forward message to {0}", clientGuid), exception)
                        ));
                    continue;
                }
                catch (Exception)
                {
                    throw;
                }

                using MemoryStream memoryStream = new(data);
                IMessageContainer payloadObject = (IMessageContainer)formatter.Deserialize(memoryStream);

                switch (payloadObject.Type)
                {
                    case "Message":
                        {
                            MessageContainer<Message> payload = (MessageContainer<Message>)payloadObject;
                            Message message = payload.Payload;
                            try
                            {
                                if (!message.Broadcast)
                                {
                                    SendUnicastMessage(message);
                                }
                                else
                                {
                                    SendBroadcastMessage(message);
                                }
                            }
                            catch (ClientDisconnectedException exception)
                            {
                                throw;
                            }
                        }
                        break;

                    case "ClientDisconnect":
                        {
                            MessageContainer<string> payload = (MessageContainer<string>)payloadObject;
                            string guid = payload.Payload;

                            //  TODO:
                            //      HANDLE CLIENT DISCONNECTION HERE
                        }
                        break;

                    case "List":
                    case "Exception":
                    default:
                        break;
                }
            }
        }

        private void Send<T>(TcpClient reciever, string recieverGuid, string type, T obj)
        {
            MessageContainer<T> payload = new(type, obj);
            NetworkStream recieverStream;

            try
            {
                 recieverStream = reciever.GetStream();            
            }
            catch (Exception exception)
            {
                if ()
                OnClientDisconnectEvent(new InternalExceptionEventArgs(
                                recieverGuid,
                                exception
                                ));
                return;
            }

            byte[] data;

            using (MemoryStream memoryStream = new())
            {
                BinaryFormatter formatter = new();
                formatter.Serialize(memoryStream, payload);
                data = memoryStream.ToArray();
            }

            byte[] dataLen = BitConverter.GetBytes(data.Length);

            lock (_lockTcpNetworkStream)
            {
                recieverStream.Write(dataLen, 0, 4);
                recieverStream.Write(data, 0, data.Length);
            }
        }

        private bool SendUnicastMessage(Message message)
        {
            TcpClient reciever;
            try
            {
                if ((reciever = GetClient(message.ReceiverClientID)) != null)
                {
                    Send(reciever, message.ReceiverClientID, "Message", message);
                    return true;
                }
                throw new KeyNotFoundException("Client does not exist.");
            }
            catch (Exception exception)
            {
                OnClientDisconnectEvent(new InternalExceptionEventArgs(
                            message.ReceiverClientID,
                            exception
                            ));
                return false;
            }
        }

        private void SendBroadcastMessage(Message message)
        {
            List<string> clientList;
            lock (_lockClientsCollection)
            {
                clientList = Clients.Keys.ToList();
            }

            foreach (string clientGuid in clientList)
            {
                if (clientGuid == message.SenderClientID)
                {
                    continue;
                }

                message.ReceiverClientID = clientGuid;

                try
                {
                    SendUnicastMessage(message);
                }
                catch (Exception) { throw; }
            }
        }

        private void BroadcastUpdatedClientLists()
        {
            List<string> keys;
            lock (_lockClientsCollection)
            {
                keys = Clients.Keys.ToList();
            }

            foreach (string clientGuid in keys)
            {
                List<string> clientList = new List<string>(keys);
                clientList.Remove(clientGuid);

                TcpClient reciever = GetClient(clientGuid);
                try
                {
                    Send(reciever, clientGuid, "ListUpdated", clientList);
                }
                catch (IOException exception)
                {
                    OnClientDisconnectEvent(new InternalExceptionEventArgs(
                            clientGuid,
                            exception
                            ));
                }
                catch(ClientDisconnectedException exception)
                {
                    OnClientDisconnectEvent(new InternalExceptionEventArgs(
                            clientGuid,
                            exception
                            ));
                }
                catch (Exception exception)
                {
                    OnThrowExceptionEvent(new InternalExceptionEventArgs(
                        clientGuid, new MessageNotSentException(string.Format("10 Could not send updated list to client {0}", clientGuid), exception)
                        ));
                }
            }
        }

        private TcpClient GetClient(string guid)
        {
            TcpClient reciever = null;
            lock (_lockClientsCollection)
            {
                Clients.TryGetValue(guid, out reciever);
            }

            if(reciever != null)
            {
                return reciever;
            }

            return null;
        }

        private void RemoveClient(string guid)
        {

            if (Clients.TryGetValue(guid, out TcpClient client))
            {
                lock (_lockClientsCollection)
                {
                    Clients.Remove(guid);
                }
            }
        }

        //  ----------------------
        //  EVENT HANDLING
        //      below are all the event handlers and event invocation methods
        //
        //      ** adding to delegate will be done outside in the console application **
        //  ----------------------

        //  WORKERS BELOW

        //private void Throw_ThrowExceptionEvent(object sender, InternalExceptionEventArgs args)
        //{
        //    throw args.ThrownException;
        //}

        //private void RemoveClient_ClientDisconnectEvent(object sender, InternalExceptionEventArgs args)
        //{
        //    if (args.ThreadGuid == null)
        //        return;

        //    bool flag;
        //    TcpClient clientForRemoval;

        //    lock (_lockClientsCollection)
        //    {
        //        flag = Clients.TryGetValue(args.ThreadGuid, out clientForRemoval);
        //    }

        //    if (flag)
        //    {
        //        using (Socket socket = clientForRemoval.Client)
        //        {
        //            if (socket.Connected)
        //            {
        //                socket.Disconnect(true);
        //            }
        //        }
        //    }

        //    try
        //    {
        //        lock (_lockClientsCollection)
        //        {
        //            Clients.Remove(args.ThreadGuid);
        //        }
        //    }
        //    catch (Exception)
        //    {
        //        // EAT UP EXCEPTION
        //    }
        //}

        //  INVOKERS BELOW

        protected virtual void OnThrowExceptionEvent(InternalExceptionEventArgs args)
        {
            ThrowExceptionEvent?.Invoke(this, args);
        }

        protected virtual void OnClientDisconnectEvent(InternalExceptionEventArgs args)
        {
            if (RemovedClients.Contains(args.ThreadGuid))
            {
                return;
            }

            lock (_lockRemovalQueue)
            {
                if (!RemoveQueue.Contains(args.ThreadGuid))
                {
                    RemoveQueue.Enqueue(args.ThreadGuid);
                }
            }
            ClientDisconnectEvent?.Invoke(this, args);
        }

        //  DELEGATES BELOW
        
        public event EventHandler<InternalExceptionEventArgs> ThrowExceptionEvent;
        public event EventHandler<InternalExceptionEventArgs> ClientDisconnectEvent;
    }
}
