﻿using Chronos.Core.Encryption;
using Chronos.Core.IO;
using Chronos.Core.Utils;
using Chronos.Protocol;
using Chronos.Protocol.Enums;
using Chronos.Protocol.Messages;
using Chronos.Server.Game.Account;
using Chronos.Server.Game.Actors.Context.Characters;
using Chronos.Server.Handlers;
using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace Chronos.Server.Network
{
    public class SimpleClient : IPacketInterceptor
    {

        #region Variables

        private byte[] sendBuffer, receiveBuffer;
        const int bufferLength = 8192;


        public MessagePart currentMessage;
        public KeyPair keyPairEncryption;
        public BigEndianReader buffer;
        public Socket Socket;
        public bool Runing { get; private set; }
        public GameAccount Account { get; set; }
        public Character Character { get; set; }
        public static int BufferSize
        {
            get
            {
                return bufferLength;
            }
        }
        public string IP
        {
            get
            {
                return ((IPEndPoint)this.Socket.RemoteEndPoint)?.Address?.ToString();
            }
        }

        #endregion

        #region Builder

        public SimpleClient(Socket socket)
        {
            Init();
            Start(socket);
            Send(new SessionKeyMessage(keyPairEncryption.Seed), false);
        }

        #endregion

        #region Methods

        public void Start(Socket socket)
        {
            try
            {
                Runing = true;
                Socket = socket;
                Socket.BeginReceive(receiveBuffer, 0, bufferLength, SocketFlags.None, new AsyncCallback(ReceiveCallBack), Socket);
            }
            catch (System.Exception ex)
            {
                OnError(new ErrorEventArgs(ex));
            }
        }

        public void Disconnect()
        {
            try
            {
                if(Socket.Connected == true)
                    Socket.BeginDisconnect(false, DisconectedCallBack, Socket);
            }
            catch (System.Exception ex)
            {
                OnError(new ErrorEventArgs(ex));
            }
        }

        public void Send(NetworkMessage message, bool encrypt = true)
        {

            BigEndianWriter writer = new BigEndianWriter();
            message.Pack(writer);
            byte[] data = writer.Data;
            if (encrypt)
            {
                writer.Seek(0);
                writer.WriteUInt((uint)writer.Data.Length);
                data = writer.Data;
                this.keyPairEncryption.Encrypt(ref data, 0, writer.Data.Length);
            }
            else
            {
                writer.Seek(0);
                writer.WriteUInt((uint)writer.Data.Length);
            }

            Send(data);
             Console.WriteLine(string.Format("[SND] {0} -> {1}", IP, message));
        }
        public void Send(byte[] data)
        {
            try
            {
                if (Socket.Connected == false)
                    Runing = false;
                if (Runing)
                {
                    if (data.Length == 0)
                        return;
                    sendBuffer = data;
                    Socket.BeginSend(sendBuffer, 0, sendBuffer.Length, SocketFlags.None, new AsyncCallback(SendCallBack), Socket);
                }
                else
                    Console.WriteLine("Send " + data.Length + " bytes but not runing");
            }
            catch (System.Exception ex)
            {
                OnError(new ErrorEventArgs(ex));
            }
        }
        public void Dispose()
        {
            // Dispose

            if(Socket != null)
                Socket.Dispose();

            //if (buffer != null)
            //    buffer.Dispose();

            // Clean

            Socket = null;
            sendBuffer = null;
            receiveBuffer = null;

        }

        #endregion

        #region Private Methods

        private void Init()
        {
            try
            {
                keyPairEncryption = new KeyPair(new Random().Next());
                buffer = new BigEndianReader();
                receiveBuffer = new byte[bufferLength];
                Socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            }
            catch (System.Exception ex)
            {
                OnError(new ErrorEventArgs(ex));
            }
        }

        public void ThreatBuffer()
        {
            if (this.currentMessage == null)
                this.currentMessage = new MessagePart();
            if (!this.currentMessage.Build(ref this.buffer, keyPairEncryption))
                return;
            this.OnDataReceived(new DataReceivedEventArgs(this.currentMessage));
        }

        #endregion

        #region CallBack

        private void ConnectionCallBack(IAsyncResult asyncResult)
        {
            try
            {
                Runing = true;
                Socket client = (Socket)asyncResult.AsyncState;
                client.EndConnect(asyncResult);
                client.BeginReceive(receiveBuffer, 0, bufferLength, SocketFlags.None, new AsyncCallback(ReceiveCallBack), client);
                OnConnected(new ConnectedEventArgs());
            }
            catch (System.Exception ex)
            {
                OnError(new ErrorEventArgs(ex));
            }
        }

        private void DisconectedCallBack(IAsyncResult asyncResult)
        {
            try
            {
                ConsoleUtils.WriteMessageInfo(string.Format("{0} is disconnected !", this));
                Runing = false;
                Socket client = (Socket)asyncResult.AsyncState;
                client.EndDisconnect(asyncResult);
                OnDisconnected(new DisconnectedEventArgs(Socket));

                this.Character?.LogOut();
                SimpleServer.RemoveClient(this);

                Dispose();
            }
            catch (System.Exception ex)
            {
                OnError(new ErrorEventArgs(ex));
            }
        }

        private void ReceiveCallBack(IAsyncResult asyncResult)
        {
            Socket client = (Socket)asyncResult.AsyncState;
            if (client.Connected == false)
            {
                Runing = false;
                return;
            }
            if (Runing)
            {
                int bytesRead = 0;
                try
                {
                    bytesRead = client.EndReceive(asyncResult);


                    if (bytesRead == 0)
                    {
                        Runing = false;
                        this.Disconnect();
                        return;
                    }
                    byte[] data = new byte[bytesRead];
                    Array.Copy(receiveBuffer, data, bytesRead);
                    buffer = new BigEndianReader(data);

                    ThreatBuffer();
                    var messagePart = DataReceivedEventArgs.Data;
                    NetworkMessage message = MessageReceiver.BuildMessage((HeaderEnum)messagePart.MessageId, buffer);
                    if (message == null)
                    {
                        ConsoleUtils.WriteWarning(string.Format("Received Unknown PacketId : {0} -> {1}", this.IP, (HeaderEnum)messagePart.MessageId));
                    }
                    else
                    {
                        Console.WriteLine(string.Format("[RCV] {0} -> {1}", this.IP, message));
                        PacketManager.ParseHandler(this, message);
                    }
                    client.BeginReceive(receiveBuffer, 0, bufferLength, SocketFlags.None, new AsyncCallback(ReceiveCallBack), client);
                }
                catch (System.Exception ex)
                {
                    ConsoleUtils.WriteError(ex.ToString());
                    this.Disconnect();
                }
            }
            else
                Console.WriteLine("Receive data but not running");
        }

        private void SendCallBack(IAsyncResult asyncResult)
        {
            try
            {
                if (Runing == true)
                {
                    Socket client = (Socket)asyncResult.AsyncState;
                    client.EndSend(asyncResult);
                    OnDataSended(new DataSendedEventArgs());
                }
                else
                    Console.WriteLine("Send data but not runing !");
            }
            catch (System.Exception ex)
            {
                OnError(new ErrorEventArgs(ex));
            }
        }

        #endregion

        #region Events

        public event EventHandler<ConnectedEventArgs> Connected;
        public event EventHandler<DisconnectedEventArgs> Disconnected;
        public event EventHandler<DataReceivedEventArgs> DataReceived;
        public event EventHandler<DataSendedEventArgs> DataSended;
        public event EventHandler<ErrorEventArgs> Error;

        private void OnConnected(ConnectedEventArgs e)
        {
            ConsoleUtils.WriteSuccess(this.ToString() + " is connected !");
            Connected?.Invoke(this, e);
        }

        private void OnDisconnected(DisconnectedEventArgs e)
        {
            Disconnected?.Invoke(this, e);
        }

        private void OnDataReceived(DataReceivedEventArgs e)
        {
            DataReceived?.Invoke(this, e);
        }

        private void OnDataSended(DataSendedEventArgs e)
        {
            DataSended?.Invoke(this, e);
        }

        private void OnError(ErrorEventArgs e)
        {
            Error?.Invoke(this, e);
        }

        #endregion

        #region EventArgs

        public class ConnectedEventArgs : EventArgs
        {
        }

        public class DisconnectedEventArgs : EventArgs
        {
            public Socket Socket { get; private set; }

            public DisconnectedEventArgs(Socket socket)
            {
                
                Socket = socket;
            }
        }

        public class DataSendedEventArgs : EventArgs
        {
        }

        public class DataReceivedEventArgs : EventArgs
        {
            public static MessagePart Data { get; private set; }

            public DataReceivedEventArgs(MessagePart data)
            {
                Data = data;
            }
        }

        public class ErrorEventArgs : EventArgs
        {
            public System.Exception Ex { get; private set; }

            public ErrorEventArgs(System.Exception ex)
            {
                Ex = ex;
            }
        }
        #endregion
        public override string ToString()
        {
            return $"client <{this.IP}>";
        }
    }
}
