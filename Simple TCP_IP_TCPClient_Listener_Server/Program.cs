using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Simple_TCP_IP_TCPClient_Listener_Server
{
    class Program
    {
        private static int _Port = 4665;
        private static TcpListener _Server;
        private static List<TcpClient> _ClientList = new List<TcpClient>();
        private static byte[] _Data = new byte[1024];

        static void Main(string[] args)
        {
            Console.Title = "Server";
            SetupServer();
            Console.ReadLine();
            foreach (TcpClient client in _ClientList)
            {
                NetworkStream clientStream = client.GetStream();
                clientStream.Close();
                client.Close();
            }
            _Server.Stop();
        }

        /// <summary>
        /// Sets up the server and starts listening
        /// </summary>
        private static void SetupServer()
        {
            Console.WriteLine("Setting up server...");
            _Server = new TcpListener(IPAddress.Loopback, _Port);
            //The listener may have one backed up connection
            _Server.Start(1);

            _Server.BeginAcceptTcpClient(new AsyncCallback(AcceptCallback), _Server);
        }

        /// <summary>
        /// Callback for when someone connects to the server
        /// </summary>
        /// <param name="ar"></param>
        private static void AcceptCallback(IAsyncResult ar)
        {
            //Get the client object from the listener
            TcpClient client = _Server.EndAcceptTcpClient(ar);
            _ClientList.Add(client);
            //Start reading from the stream
            NetworkStream clientStream = client.GetStream();
            clientStream.BeginRead(_Data, 0, _Data.Length, new AsyncCallback(ReadCallback), client);
            //Start accepting connections again
            _Server.BeginAcceptTcpClient(new AsyncCallback(AcceptCallback), _Server);
        }

        /// <summary>
        /// Callback for when a client sends data
        /// </summary>
        /// <param name="ar"></param>
        private static void ReadCallback(IAsyncResult ar)
        {
            //Specify the client (is the same as client in AcceptCallBack() thus the same client)
            TcpClient client = (TcpClient)ar.AsyncState;
            NetworkStream clientStream = client.GetStream();
            //Trim the received data so we dont get null bytes
            int received = clientStream.EndRead(ar);
            byte[] dataBuffer = new byte[received];
            Array.Copy(_Data, dataBuffer, received);

            string text = Encoding.ASCII.GetString(dataBuffer);
            Console.WriteLine($@"Received {text}");

            //Commands for the server
            if (text.ToLower() == "exit")
            {
                clientStream.Close();
                client.Close();
                _ClientList.Remove(client);
                Console.WriteLine("Client disconnected");
                return;
            }
            else if (text.ToLower() == "get time")
            {
                SendText(DateTime.Now.ToString(), client);
            }
            else
            {
                SendText("Invalid command", client);
            }
        }

        /// <summary>
        /// Function to start sending data
        /// </summary>
        /// <param name="text"></param>
        /// <param name="clientStream"></param>
        private static void SendText(string text, TcpClient client)
        {
            byte[] data = Encoding.ASCII.GetBytes(text);
            NetworkStream clientStream = client.GetStream();
            clientStream.BeginWrite(data, 0, data.Length, new AsyncCallback(SendCallback), client);
        }

        /// <summary>
        /// Callback for when the data is completed sending
        /// </summary>
        /// <param name="ar"></param>
        private static void SendCallback(IAsyncResult ar)
        {
            TcpClient client = (TcpClient)ar.AsyncState;
            NetworkStream clientStream = client.GetStream();
            clientStream.EndWrite(ar);
            clientStream.BeginRead(_Data, 0, _Data.Length, new AsyncCallback(ReadCallback), client);
        }
    }
}
