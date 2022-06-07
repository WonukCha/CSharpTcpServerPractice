using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Concurrent;
using System.Diagnostics;


namespace TcpServerTest
{
    public class TcpServer
    {
        public virtual void OnStart() { }
        public virtual void OnStop() { }
        public virtual void OnConnect(uint clientIndex , string ipEndPoint) { }
        public virtual void OnDisconnect(uint clientIndex) { }
        public virtual async void OnReceive(uint clientIndex, byte[] buffer, int offset, int count) { }
        public virtual void OnSend(uint clientIndex, int size) { }

        public void Init(int port, uint maxSessionCount)
        {
            Port = port;
            MaxSessionCount = maxSessionCount;
            _tcpSession = new TcpSession[MaxSessionCount];

            for (uint i = 0; i < MaxSessionCount; i++)
            {
                _tcpSession[i] = new TcpSession();
                _tcpSession[i].Init(this,i);
            }
            
        }

        public int Port { get; private set; }

        public uint MaxSessionCount { get; private set; }

        public int OptionReceiveBufferSize { get; set; } = 8192;

        public int OptionSendBufferSize { get; set; } = 8192;

        public int OptionMaxPacketSize { get; set; } = 1024;

        public bool IsStarted { get; private set; }

        public bool IsRun { get; private set; }

        public bool Start()
        {
            Debug.Assert(!IsStarted, "TCP server is already started!");
            if (IsStarted)
                return false;

            IsRun = true;
            _tcpListener = CreateTcpListener();
            _tcpListener.Start();

            Task task = AysncAcceptTcpServerProcess();
            IsStarted = true;

            OnStart();

            return true;
        }

        public bool Stop()
        {
            Debug.Assert(IsStarted, "Server is not started!");
            if (!IsStarted)
                return false;
             
            IsRun = false;
            try
            {
                _tcpListener.Stop();
            }
            catch (ObjectDisposedException) { }

            DisconnectAll();

            IsStarted = false;
            OnStop();

            return true;
        }

        public bool Restart()
        {
            if (!Stop())
                return false;

            while (IsStarted)
                Thread.Yield();

            return Start();
        }

        public bool DisconnectAll()
        {
            if (!IsStarted)
                return false;

            foreach (var session in _tcpSession)
                session.Disconnect();

            return true;
        }
        public bool SendToAll(byte[] buffer, int offset, int size)
        {
            if (!IsStarted)
                return false;

            for (uint i = 0; i < MaxSessionCount; i++)
            {
                if(_tcpSession[i].IsConnected)
                {
                    SendToClient(i, buffer, offset, size);
                }
            }
            return true;
        }

        public bool SendToClient(uint clientIndex,byte[] buffer, int offset, int size)
        {
            if(!IsStarted)
                return false;
            if(clientIndex >= _tcpSession.Length)
                return false;

            _tcpSession[clientIndex].SendToData(buffer, offset, size);
            OnSend(clientIndex, size);
            return true;
        }

        public int GetReceiveSize(uint clientIndex)
        {
            return _tcpSession[clientIndex].GetReceiveBufferSize();
        }

        public async Task<ArraySegment<byte>> ReadReceiveData(uint clientIndex,int readSize,bool delete = true)
        {
            var task = Task.Run(() => _tcpSession[clientIndex].GetReceiveBuffer(readSize, delete));
            ArraySegment<byte> result = await task;
            return result;
        }

        async Task AysncAcceptTcpServerProcess()
        {
            while(IsRun)
            {
                TcpClient tc = await _tcpListener.AcceptTcpClientAsync().ConfigureAwait(false);
                TcpSession ts = null;
                for (uint i = 0;i<MaxSessionCount;i++)
                {
                    if(_tcpSession[i].IsConnected == false)
                    {
                        _tcpSession[i].Connect(tc);
                        ts = _tcpSession[i];
                        break;
                    }
                }

                if(ts == null)
                {
                    //Session Full
                    tc.Close();
                }
                else
                {
                    Task.Factory.StartNew(AsyncTcpReceiveProcess, ts);
                    OnConnect(ts.SessionIndex, tc.Client.RemoteEndPoint.ToString());
                }
            }
        }
        async void AsyncTcpReceiveProcess(object o)
        {
            TcpSession ts = (TcpSession)o;
            TcpClient tc = ts.TcpClient;
            NetworkStream stream = tc.GetStream();
            CancellationTokenSource cancelTokenSource = ts.CancelTokenSource;
            // 비동기 수신            
            var buff = new byte[OptionReceiveBufferSize];
            Task<int> readTask;

            while (IsRun)
            {
                int nbytes = 0;
                cancelTokenSource.Token.Register(() => stream.Close());
                readTask = stream.ReadAsync(buff, 0, buff.Length, cancelTokenSource.Token);

                var timeoutTask = Task.Delay(60 * 1000);  // 60 secs
                var doneTask = await Task.WhenAny(timeoutTask, readTask).ConfigureAwait(false);

                if(readTask.IsFaulted && cancelTokenSource.IsCancellationRequested) // 서버에서 종료할때.
                {
                    break;
                }

                if (doneTask == timeoutTask) // 타임아웃이면
                {
                    break;
                }
                else  // 수신 성공이면
                {
                    nbytes = readTask.Result;
                    if (nbytes > 0)
                    {
                        _tcpSession[ts.SessionIndex].ReceiveData(buff, 0, nbytes);
                        OnReceive(ts.SessionIndex, buff, 0, nbytes);
                    }
                    else // 상대방이 끊었을때.
                    {
                        break;
                    }
                }
            }
            stream.Close();
            tc.Close();
            ts.Disconnect();
            OnDisconnect(ts.SessionIndex);
        }
        private TcpListener _tcpListener;
        private TcpSession[] _tcpSession;

        protected TcpListener CreateTcpListener()
        {
            return new TcpListener(IPAddress.Any, Port);
        }
    }
}
