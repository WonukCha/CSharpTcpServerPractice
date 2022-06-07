using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace TcpServerTest
{
    class TcpSession
    {
        public void Init(TcpServer server, uint index)
        {
            Server = server;
            OptionReceiveBufferSize = server.OptionReceiveBufferSize;
            OptionSendBufferSize = server.OptionSendBufferSize;
            OptionMaxPacketSize = server.OptionMaxPacketSize;
            SessionIndex = index;

            _receiveBuffer = new BufferManager();
            _sendBufferMain = new BufferManager();
            _sendBufferFlush = new BufferManager();

            _receiveBuffer.Init(OptionReceiveBufferSize, OptionMaxPacketSize);
            _sendBufferMain.Init(OptionReceiveBufferSize, OptionMaxPacketSize);
            _sendBufferFlush.Init(OptionReceiveBufferSize, OptionMaxPacketSize);

        }

        public TcpServer Server { get; private set; }

        public TcpClient TcpClient { get; private set; }

        public uint SessionIndex { get; private set; }

        public int OptionReceiveBufferSize { get; set; } = 8192;

        public int OptionSendBufferSize { get; set; } = 8192;

        public int OptionMaxPacketSize { get; set; } = 1024;

        public bool IsConnected { get; private set; }

        public CancellationTokenSource CancelTokenSource { get; private set; }

        public void Connect(TcpClient tcpClient)
        {
            TcpClient = tcpClient;
            CancelTokenSource = new CancellationTokenSource();



            //if (Server.OptionKeepAlive)
            //    Socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
            // Apply the option: no delay
            //if (Server.OptionNoDelay)
            //    Socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, true);

            ClearSendBuffers();
            ClearReceiveBuffers();

            // Update the connected flag
            IsConnected = true;

            // Call the empty send buffer handler
            //if (_sendBufferMain.IsEmpty)
            //OnEmpty();
        }

        public bool Disconnect()
        {
            if (!IsConnected)
                return false;

            try
            {
                //try
                //{
                //    // Shutdown the socket associated with the client
                //    //TcpClient.Shutdown(SocketShutdown.Both);
                //}
                //catch (SocketException) { }
                CancelTokenSource.Cancel();
            }
            catch (ObjectDisposedException) { }

            // Update the connected flag
            TcpClient = null;
            IsConnected = false;
            
            // Clear send/receive buffers
            ClearSendBuffers();
            ClearReceiveBuffers();

            return true;
        }

        public async void SendToData(byte[] buffer, int offset, int size)
        {
            if (!IsConnected)
                return;
            NetworkStream ns = TcpClient.GetStream();
            if (!ns.CanWrite)
                return;
            //if (ns.Length > OptionSendBufferSize)
            //    return;
            try
            {
                await ns.WriteAsync(buffer, offset, size).ConfigureAwait(false);
            }
            catch (Exception e) 
            { 
                return; 
            }
            return;
        }
        public int GetReceiveBufferSize()
        {
            return _receiveBuffer.Size();
        }

        public ArraySegment<byte> GetReceiveBuffer(int readSize, bool delete = true)
        {
            ArraySegment<byte> result;
            lock (_ReceiveLock)
            {
                result = _receiveBuffer.Read(readSize, delete);
            }
            return result;
        }

        public void ReceiveData(byte[] buffer, int offset, int size)
        {
            lock (_ReceiveLock)
            {
                _receiveBuffer.Write(buffer, offset, size);
            }
        }

        private void ClearSendBuffers()
        {
            lock (_sendLock)
            {
                // Clear send buffers
                _sendBufferMain.Clear();
                _sendBufferFlush.Clear();
            }
        }

        private void ClearReceiveBuffers()
        {
            lock (_ReceiveLock)
            {
                // Clear receive buffers
                _receiveBuffer.Clear();
            }
        }

        private BufferManager _receiveBuffer = null;
        private BufferManager _sendBufferMain = null;
        private BufferManager _sendBufferFlush = null;
        private readonly object _sendLock = new object();
        private readonly object _ReceiveLock = new object();
        
    }
}
