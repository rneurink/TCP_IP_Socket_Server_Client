using System;
using System.Text;
using System.Net.Sockets;

namespace Simple_TCP_IP_Socket_Client
{
    /* Simple TCP IP client that is able to connect to a server and send commands
     * This client is completely synchronous.
     */
    class Program
    {
        private static Socket _ClientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        private static int _Port = 4665;

        static void Main(string[] args)
        {
            Console.Title = "Client";
            Console.Write("Enter ip address: ");
            string ip = Console.ReadLine();
            //Connect to the server. While not connected do not continue
            LoopConnect(ip);
            //Send commands to the server. This will loop
            LoopSend();
            //Safety so the main thread does not die
            Console.ReadLine();
        }

        private static void LoopSend()
        {
            while (_ClientSocket.Connected)
            {
                Console.Write("Enter a command: ");
                string command = Console.ReadLine();
                byte[] data = Encoding.ASCII.GetBytes(command);
                //Send a command
                _ClientSocket.Send(data);
                if (command.ToLower() == "exit")
                {
                    _ClientSocket.Shutdown(SocketShutdown.Both);
                    _ClientSocket.Close();
                    Environment.Exit(0);
                }

                //Wait for the server to reply
                byte[] receivebuffer = new byte[1024];
                int received = _ClientSocket.Receive(receivebuffer);
                byte[] receiveddata = new byte[received];
                Array.Copy(receivebuffer, receiveddata, received);
                Console.WriteLine($@"Received: {Encoding.ASCII.GetString(receiveddata)}");
            }
        }

        private static void LoopConnect(string ip)
        {
            int connectionAttempts = 0;
            while (!_ClientSocket.Connected)
            {
                try
                {
                    connectionAttempts++;
                    _ClientSocket.Connect(ip, _Port);
                }
                catch (SocketException)
                {
                    //Socket exception can occur when not able to connect
                    Console.Clear();
                    Console.WriteLine($@"Connection failed, attempts:{connectionAttempts}");
                }
            }
            Console.Clear();
            Console.WriteLine("Connected");
        }
    }
}
