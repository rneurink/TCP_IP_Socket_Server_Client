using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Simple_TCP_IP_TCPClient_Listener_Client
{
    /* Simple TCPClient that is able to connect to a server and send commands
     * This client is completely synchronous.
     * The use of TCPClient is pretty much the same as a socket on the Client side.
     */
    class Program
    {
        private static int _Port = 4665;
        private static TcpClient _Client;
        private static NetworkStream _ClientStream;

        static void Main(string[] args)
        {
            Console.Title = "Client";
            Console.Write("Enter ip address: ");
            string ip = Console.ReadLine();
            _Client = new TcpClient();
            LoopConnect(ip);
            LoopSend();
        }

        private static void LoopSend()
        {
            while (_Client.Connected)
            {
                Console.Write("Enter a command: ");
                string command = Console.ReadLine();
                byte[] data = Encoding.ASCII.GetBytes(command);
                //Send a command
                _ClientStream.Write(data, 0, data.Length);
                if (command.ToLower() == "exit")
                {
                    _ClientStream.Close();
                    _Client.Close();
                    Environment.Exit(0);
                }

                //Wait for the server to reply
                byte[] receivebuffer = new byte[1024];
                int received = _ClientStream.Read(receivebuffer, 0, receivebuffer.Length);
                byte[] receiveddata = new byte[received];
                Array.Copy(receivebuffer, receiveddata, received);
                Console.WriteLine($@"Received: {Encoding.ASCII.GetString(receiveddata)}");
            }
        }

        private static void LoopConnect(string ip)
        {
            int connectionAttempts = 0;
            while (!_Client.Connected)
            {
                try
                {
                    connectionAttempts++;
                    _Client.Connect(ip, _Port);
                }
                catch (SocketException)
                {
                    //Socket exception can occur when not able to connect
                    Console.Clear();
                    Console.WriteLine($@"Connection failed, attempts:{connectionAttempts}");
                }
            }
            _ClientStream = _Client.GetStream();
            Console.Clear();
            Console.WriteLine("Connected");
        }
    }
}
