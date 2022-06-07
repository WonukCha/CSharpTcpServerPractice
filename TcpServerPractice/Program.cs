using System;
using TcpServerTest;

namespace TcpServerPractice
{
    class Program
    {
        static void Main(string[] args)
        {
            TcpServer tcpServer = new TcpServer();
            tcpServer.Init(10000, 10);
            tcpServer.Start();
            tcpServer.Stop();
        }
    }
}
