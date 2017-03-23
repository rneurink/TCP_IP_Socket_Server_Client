using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Windows;
using System.Windows.Input;

namespace Advanced_TCP_IP_Socket_Server
{
    public enum ClientState
    {
        Error = -1,
        NotLoggedin = 0,
        LoggedIn = 1
    }

    public class Client
    {
        public IPEndPoint ClientEndpoint;
        public DateTime ConnectedAt;
        public ClientState State;

        public Client(IPEndPoint ClientEndpoint, DateTime ConnectedAt, ClientState State)
        {
            this.ClientEndpoint = ClientEndpoint;
            this.ConnectedAt = ConnectedAt;
            this.State = State;
        }
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private static int _Port;
        private static Socket _ServerSocket;
        private static Dictionary<Socket, Client> _ClientList = new Dictionary<Socket, Client>();
        private static Dictionary<string, string> _UserList = new Dictionary<string, string>();
        private static bool _NewClients = true;
        private static byte[] _Data;

        public MainWindow()
        {
            InitializeComponent();

            // Available login credentials
            _UserList.Add("Admin", "Admin");

            StartServerButton.Click += StartServerButton_Click;
            StopServerButton.Click += StopServerButton_Click;

            SendTB.KeyDown += SendTB_KeyDown;
            SendButton.Click += SendButton_Click;
        }

        #region UIHandlers
        /// <summary>
        /// Sends a command to the internal server or to the client
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SendButton_Click(object sender, RoutedEventArgs e)
        {
            string command = SendTB.Text.ToLower();
            if (command == "help")
            {
                ServerLog("Available commands:\n Help - Displays this message\n Lock - Stop accepting new clients\n Unlock - Start accepting new clients\n ListClients - Lists connected clients\n Kill <ClientID> - Kills a client connection\n KillAll - Kills all client connections\n SendAll <Message> - Sends a message to all clients\n Send <ClientID> <Message> - Sends a message to the specified client");
            }
            else if (command == "lock")
            {
                ServerLog("Stopped accepting new clients");
                _NewClients = false;
            }
            else if (command == "unlock")
            {
                ServerLog("Started accepting new clients");
                _NewClients = true;
                try
                {
                    _ServerSocket.BeginAccept(_ServerSocket.ReceiveBufferSize, AcceptCallBack, _ServerSocket);
                }
                catch (SocketException ex)
                {
                    ServerLog("Unlock " + ex.Message);
                }
                catch (ObjectDisposedException ex)
                {
                    ServerLog("Unlock  " + ex.Message);
                }
            }
            else if (command == "listclients")
            {
                string output = "Clients:\n";

                if (_ClientList.Count == 0)
                {
                    output += "No clients connected";
                }
                else
                {
                    int ClientID = 0;
                    foreach (KeyValuePair<Socket, Client> client in _ClientList)
                    {
                        Client _client = client.Value;
                        if (ClientID != 0) output += "\n";
                        output += $@" Client #{ClientID}: {_client.ClientEndpoint.Address.ToString()}:{_client.ClientEndpoint.Port}, Connected at: {_client.ConnectedAt}";
                        ClientID++;
                    }
                }

                ServerLog(output);
            }
            else if (command == "killall")
            {
                int killedClients = 0;
                try
                {
                    foreach (Socket socket in _ClientList.Keys.ToArray())
                    {
                        socket.Shutdown(SocketShutdown.Both);
                        socket.Close();
                        _ClientList.Remove(socket);
                        killedClients++;
                    }
                }
                catch (SocketException ex)
                {
                    ServerLog("KillAll " + ex.Message);
                }
                catch (ObjectDisposedException ex)
                {
                    ServerLog("KillAll " + ex.Message);
                }
                finally
                {
                    ServerLog($@"{killedClients} clients were forcefully disconnected");
                }
            }
            else if (command.StartsWith("kill"))
            {
                string[] clientid = command.Split(' ');
                int ClientID = -1;
                try
                {
                    if (int.TryParse(clientid[1], out ClientID) && ClientID <= _ClientList.Count)
                    {
                        int selectedClientID = 0;
                        foreach (Socket socket in _ClientList.Keys)
                        {
                            if (selectedClientID == ClientID)
                            {
                                socket.Shutdown(SocketShutdown.Both);
                                socket.Close();
                                _ClientList.Remove(socket);
                                ServerLog($@"Client #{ClientID} was forcefully disconnected");
                                break;
                            }
                            selectedClientID++;
                        }
                    }
                }
                catch
                {
                    ServerLog($@"Could not kick client #{ClientID}");
                }
            }
            else if (command.StartsWith("sendall"))
            {
                string[] message = command.Split(' ');
                try
                {
                    byte[] sendbuffer = Encoding.ASCII.GetBytes(message[1].Trim());
                    foreach(Socket socket in _ClientList.Keys)
                    {
                        socket.BeginSend(sendbuffer, 0, sendbuffer.Length, SocketFlags.None, SendCallback, socket);
                    }
                    ServerLog($@"Send to all clients: {message[1]}");
                }
                catch (SocketException ex)
                {
                    ServerLog("SendAll " + ex.Message);
                }
                catch (ObjectDisposedException ex)
                {
                    ServerLog("SendAll " + ex.Message);
                }
            }
            else if (command.StartsWith("send"))
            {
                string[] idmessage = command.Split(' ');
                int ClientID = -1;
                try
                {
                    if (int.TryParse(idmessage[1], out ClientID) && ClientID <= _ClientList.Count)
                    {
                        byte[] sendbuffer = Encoding.ASCII.GetBytes(idmessage[2].Trim());
                        int selectedClientID = 0;
                        foreach (Socket socket in _ClientList.Keys)
                        {
                            if (selectedClientID == ClientID)
                            {
                                socket.BeginSend(sendbuffer, 0, sendbuffer.Length, SocketFlags.None, SendCallback, socket);
                                ServerLog($@"Send to client #{ClientID}: {idmessage[2]}");
                                break;
                            }
                            selectedClientID++;
                        }
                    }
                }
                catch (SocketException ex)
                {
                    ServerLog("Send " + ex.Message);
                }
                catch (ObjectDisposedException ex)
                {
                    ServerLog("Send " + ex.Message);
                }
            }
            else
            {
                ServerLog("Invalid command");
            }
        }

        /// <summary>
        /// When the user pressed enter do the same as the buttonclick
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SendTB_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                SendButton_Click(this, null);
            }
        }

        /// <summary>
        /// Stops the server and disconnects all clients
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void StopServerButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ServerLog("Server shutting down");

                if (_ClientList.Keys.Count > 0)
                {
                    foreach (Socket socket in _ClientList.Keys)
                    {
                        byte[] buffer = Encoding.ASCII.GetBytes("Server shutting down");
                        socket.Send(buffer, 0, buffer.Length, SocketFlags.None);
                        socket.Disconnect(false);
                    }
                }
                if (_ServerSocket.Connected)
                {
                    _ServerSocket.Shutdown(SocketShutdown.Both);
                }
                _ServerSocket.Close();
                EnableServerControls(false);
                ServerLog($@"{DateTime.Now} Server stopped");
            }
            catch (SocketException ex)
            {
                ServerLog("StopServerButton_Click " + ex.Message);
            }
            catch (ObjectDisposedException ex)
            {
                ServerLog("StopServerButton_Click " + ex.Message);
            }
        }

        /// <summary>
        /// Starts the server 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void StartServerButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                EnableServerControls(true);
                _Port = (int)PortUpDown.Value;
                ServerLog($@"Starting server on port {_Port}");
                _ServerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                _ServerSocket.Bind(new IPEndPoint(IPAddress.Any, _Port));
                _ServerSocket.Listen(5);
                _ServerSocket.BeginAccept(_ServerSocket.ReceiveBufferSize, AcceptCallBack, _ServerSocket);
                ServerLog($@"{DateTime.Now} Server started");
                ServerLog("Use help to list all available commands");
                _Data = new byte[1024];
            }
            catch (SocketException ex)
            {
                ServerLog("StartServerButton_Click " + ex.Message);
            }
            catch (ObjectDisposedException ex)
            {
                ServerLog("StartServerButton_Click " + ex.Message);
            }
        }

        /// <summary>
        /// When the server is started stop the server
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_ServerSocket != null)
            {
                if (_ServerSocket.IsBound) StopServerButton_Click(this, null);
            }
        }
        #endregion

        #region HelperFunctions
        /// <summary>
        /// Appends text to the log and autoscrolls if needed
        /// </summary>
        /// <param name="message"></param>
        private void ServerLog(string message)
        {
            ConsoleTB.Dispatcher.Invoke(new Action(() =>
            {
                ConsoleTB.AppendText(message + Environment.NewLine);
                ConsoleTB.Focus();
                ConsoleTB.CaretIndex = ConsoleTB.Text.Length;
                ConsoleTB.ScrollToEnd();
            }));
        }

        /// <summary>
        /// Enables or disables certain controls for when the server is started or stopped
        /// </summary>
        /// <param name="enable"></param>
        private void EnableServerControls(bool enable)
        {
            StartServerButton.Dispatcher.Invoke(new Action(() => StartServerButton.IsEnabled = !enable));
            StopServerButton.Dispatcher.Invoke(new Action(() => StopServerButton.IsEnabled = enable));
            PortUpDown.Dispatcher.Invoke(new Action(() => PortUpDown.IsEnabled = !enable));
            CommandsGB.Dispatcher.Invoke(new Action(() => CommandsGB.IsEnabled = enable));
        }
        #endregion

        #region Callbacks
        /// <summary>
        /// Callback from when someone connects. Also checks credentials.
        /// </summary>
        /// <param name="ar"></param>
        private void AcceptCallBack(IAsyncResult ar)
        {
            try
            {
                if (!_NewClients) return;
                byte[] buffer = new byte[_ServerSocket.ReceiveBufferSize];
                Socket clientSocket = _ServerSocket.EndAccept(out buffer, ar);
                string output = Encoding.ASCII.GetString(buffer);
                IPEndPoint clientendpoint = (IPEndPoint)clientSocket.RemoteEndPoint;

                string username = output.Split(',').First();
                string password = output.Split(',').Last();

                string correctpassword = null;
                _UserList.TryGetValue(username, out correctpassword);

                byte[] sendbuffer;
                if (password != correctpassword)
                {
                    ServerLog($@"Client connection refused {clientendpoint.Address.ToString()}:{clientendpoint.Port}");

                    sendbuffer = Encoding.ASCII.GetBytes("Invalid username or password, refusing connection");
                    clientSocket.Send(sendbuffer, 0, sendbuffer.Length, SocketFlags.None);
                    clientSocket.Shutdown(SocketShutdown.Both);
                    clientSocket.Close();
                    _ServerSocket.BeginAccept(_ServerSocket.ReceiveBufferSize, AcceptCallBack, _ServerSocket);
                    return;
                }

                Client client = new Client(clientendpoint, DateTime.Now, ClientState.LoggedIn);
                _ClientList.Add(clientSocket, client);

                ServerLog($@"Client {username} connected from {client.ClientEndpoint.Address.ToString()}:{client.ClientEndpoint.Port}");

                sendbuffer = Encoding.ASCII.GetBytes("Succesfully connected, use help to list all available commands");

                clientSocket.BeginSend(sendbuffer, 0, sendbuffer.Length, SocketFlags.None, SendCallback, clientSocket);
                clientSocket.BeginReceive(_Data, 0, _Data.Length, SocketFlags.None, ReceiveCallback, clientSocket);

                _ServerSocket.BeginAccept(_ServerSocket.ReceiveBufferSize, AcceptCallBack, _ServerSocket);
            }
            catch (SocketException ex)
            {
                ServerLog("AcceptCallBack " + ex.Message);
            }
            catch (ObjectDisposedException ex)
            {
                ServerLog("AcceptCallBack " + ex.Message);
            }
        }

        /// <summary>
        /// Callback for when something is received from a client. Also handles commands
        /// </summary>
        /// <param name="ar"></param>
        private void ReceiveCallback(IAsyncResult ar)
        {
            Socket clientSocket = (Socket)ar.AsyncState;
            try
            {
                int received = clientSocket.EndReceive(ar);

                Client client;
                _ClientList.TryGetValue(clientSocket, out client);

                if (received == 0)
                {
                    clientSocket.Close();
                    _ClientList.Remove(clientSocket);
                    ServerLog($@"Client disconnected {client.ClientEndpoint.Address.ToString()}:{client.ClientEndpoint.Port}");
                    return;
                }
                byte[] receivebuffer = new byte[received];
                Array.Copy(_Data, receivebuffer, received);
                string text = Encoding.ASCII.GetString(receivebuffer);
                ServerLog($@"Received {text} from {client.ClientEndpoint.Address.ToString()}");

                string sendtext;
                if (text.ToLower() == "help")
                {
                    sendtext = "Available commands:\n Help - Displays this message\n Get Time - Gets the time of the server\n Send <Message> - Sends a message to the server";
                }
                else if (text.ToLower() == "exit")
                {
                    clientSocket.Shutdown(SocketShutdown.Both);
                    clientSocket.Close();
                    _ClientList.Remove(clientSocket);
                    ServerLog($@"Client {client.ClientEndpoint.Address.ToString()} disconnected");
                    return;
                }
                else if (text.ToLower() == "get time")
                {
                    sendtext = DateTime.Now.ToString();
                }
                else if (text.ToLower().StartsWith("send"))
                {
                    ServerLog($@"Client send: {text.Split(' ').Last()}");
                    sendtext = "Message received";
                }
                else
                {
                    sendtext = "Invalid command";
                }

                byte[] sendbuffer = Encoding.ASCII.GetBytes(sendtext);
                clientSocket.BeginSend(sendbuffer, 0, sendbuffer.Length, SocketFlags.None, SendCallback, clientSocket);

                clientSocket.BeginReceive(_Data, 0, _Data.Length, SocketFlags.None, ReceiveCallback, clientSocket);
            }
            catch (SocketException ex)
            {
                ServerLog("ReceiveCallback " + ex.Message);
                //Socket exception when client dies
                clientSocket.Close();
                _ClientList.Remove(clientSocket);
            }
            catch (ObjectDisposedException ex)
            {
                ServerLog("ReceiveCallback " + ex.Message);
            }
        }

        /// <summary>
        /// Callback for when all data is send
        /// </summary>
        /// <param name="ar"></param>
        private void SendCallback(IAsyncResult ar)
        {
            try
            {
                Socket clientSocket = (Socket)ar.AsyncState;
                clientSocket.EndSend(ar);
            }
            catch (SocketException ex)
            {
                ServerLog("SendCallback " + ex.Message);
            }
            catch (ObjectDisposedException ex)
            {
                ServerLog("SendCallback " + ex.Message);
            }
        }
        #endregion
    }
}
