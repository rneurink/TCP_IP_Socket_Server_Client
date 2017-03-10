using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;

namespace Simple_TCP_IP_Socket_Server
{
    /* Simple TCP IP server that is able to handle commands send from a client.
     * Server is able to handle multiple clients
     * AsyncCallbacks make it possible to process a client while busy with another task
     * All socket related functions are static, this means these functions cannot be initialized.
     * The static modifier is necessary for the Socket.
    */
    class Program
    {
        private static Socket _ServerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        private static int _Port = 4665;
        private static List<Socket> _ClientSockets = new List<Socket>();
        private static byte[] _Data = new byte[1024];

        static void Main(string[] args)
        {
            Console.Title = "Server";
            SetupServer();
            Console.ReadLine();
        }

        /// <summary>
        /// Sets up the server and starts listening
        /// </summary>
        private static void SetupServer()
        {
            Console.WriteLine("Setting up server...");
            //Bind the socket to an IP end point
            _ServerSocket.Bind(new IPEndPoint(IPAddress.Any, _Port));
            //The socket may have one backed up connection
            _ServerSocket.Listen(1);

            //Start accepting connections
            _ServerSocket.BeginAccept(new AsyncCallback(AcceptCallback), _ServerSocket);
        }

        /// <summary>
        /// Callback for when someone connects to the server
        /// </summary>
        /// <param name="result"></param>
        private static void AcceptCallback(IAsyncResult result)
        {
            //Create a new socket to free up the server socket
            Socket socket = _ServerSocket.EndAccept(result);
            _ClientSockets.Add(socket);
            //Start receiving on the socket
            socket.BeginReceive(_Data, 0, _Data.Length, SocketFlags.None, new AsyncCallback(ReceiveCallback), socket);
            Console.WriteLine("New client connected");
            //Start accepting connections again
            _ServerSocket.BeginAccept(new AsyncCallback(AcceptCallback), _ServerSocket);
        }

        /// <summary>
        /// Callback for when a client sends data
        /// </summary>
        /// <param name="result"></param>
        private static void ReceiveCallback(IAsyncResult result)
        {
            //Specify the socket (is the same as socket in AcceptCallBack() thus the client socket)
            Socket socket = (Socket)result.AsyncState;
            //Trim the received data so we dont get null bytes
            int received = socket.EndReceive(result);
            byte[] dataBuffer = new byte[received];
            Array.Copy(_Data, dataBuffer, received);

            string text = Encoding.ASCII.GetString(dataBuffer);
            Console.WriteLine($@"Received {text}");

            //Commands for the server
            if (text.ToLower() == "get time")
            {
                SendText(DateTime.Now.ToString(), socket);
            }
            else
            {
                SendText("Invalid command", socket);
            }
        }

        /// <summary>
        /// Function to start sending data
        /// </summary>
        /// <param name="text"></param>
        /// <param name="socket"></param>
        private static void SendText(string text, Socket socket)
        {
            byte[] data = Encoding.ASCII.GetBytes(text);
            socket.BeginSend(data, 0, data.Length, SocketFlags.None, new AsyncCallback(SendCallback), socket);
        }

        /// <summary>
        /// Callback for when the data is completed sending
        /// </summary>
        /// <param name="result"></param>
        private static void SendCallback(IAsyncResult result)
        {
            Socket socket = (Socket)result.AsyncState;
            socket.EndSend(result);
            socket.BeginReceive(_Data, 0, _Data.Length, SocketFlags.None, new AsyncCallback(ReceiveCallback), socket);
        }
    }
}
