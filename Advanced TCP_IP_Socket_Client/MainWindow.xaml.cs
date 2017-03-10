using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Windows;

namespace Advanced_TCP_IP_Socket_Client
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private static int _Port;
        private static byte[] _Data;
        private static Socket _ClientSocket;
        private static string username;
        private static string password;

        public MainWindow()
        {
            InitializeComponent();

            ConnectButton.Click += ConnectButton_Click;
            DisconnectButton.Click += DisconnectButton_Click;
            SendButton.Click += SendButton_Click;
        }

        #region UIHandlers
        /// <summary>
        /// Sends a command to the server
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SendButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                byte[] sendBuffer = Encoding.ASCII.GetBytes(SendTB.Text);

                if (SendTB.Text.ToLower() == "exit")
                {
                    _ClientSocket.Send(sendBuffer, 0, sendBuffer.Length, SocketFlags.None);
                    _ClientSocket.Shutdown(SocketShutdown.Both);
                    _ClientSocket.Close();
                    EnableClientControls(false);
                    return;
                }
                _ClientSocket.BeginSend(sendBuffer, 0, sendBuffer.Length, SocketFlags.None, SendCallback, _ClientSocket);
            }
            catch (SocketException ex)
            {
                ClientLog("SendButton_Click " + ex.Message);
            }
            catch (ObjectDisposedException ex)
            {
                ClientLog("SendButton_Click " + ex.Message);
            }
        }

        /// <summary>
        /// Disconnects from the server
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DisconnectButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                byte[] sendBuffer = Encoding.ASCII.GetBytes("exit");

                _ClientSocket.Send(sendBuffer, 0, sendBuffer.Length, SocketFlags.None);

                if (_ClientSocket.Connected)
                {
                    _ClientSocket.Shutdown(SocketShutdown.Both);
                }
                _ClientSocket.Close();
                EnableClientControls(false);
            }
            catch (SocketException ex)
            {
                ClientLog("DisconnectButton_Click " + ex.Message);
            }
            catch (ObjectDisposedException ex)
            {
                ClientLog("DisconnectButton_Click " + ex.Message);
            }
        }

        /// <summary>
        /// Try to connect to the server
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _Port = (int)PortUpDown.Value;
                ClientLog($@"Trying to connect to {IpTB.Text}:{_Port}");
                username = UserTB.Text;
                password = PassTB.Text;
                _ClientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                _ClientSocket.BeginConnect(new IPEndPoint(IPAddress.Parse(IpTB.Text), _Port), ConnectCallback, _ClientSocket);
                _Data = new byte[1024];
            }
            catch (SocketException ex)
            {
                ClientLog("ConnectButton_Click " + ex.Message);
            }
            catch (ObjectDisposedException ex)
            {
                ClientLog("ConnectButton_Click " + ex.Message);
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_ClientSocket != null)
            {
                if (_ClientSocket.Connected) DisconnectButton_Click(this, null);
            }
        }
        #endregion

        #region HelperFunctions
        /// <summary>
        /// Appends text to the log and autoscrolls if needed
        /// </summary>
        /// <param name="message"></param>
        private void ClientLog(string message)
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
        /// Enables or disables certain controls for when the client is connected or disconnected
        /// </summary>
        /// <param name="enable"></param>
        private void EnableClientControls(bool enable)
        {
            ConnectButton.Dispatcher.Invoke(new Action(() => ConnectButton.IsEnabled = !enable));
            DisconnectButton.Dispatcher.Invoke(new Action(() => DisconnectButton.IsEnabled = enable));
            PortUpDown.Dispatcher.Invoke(new Action(() => PortUpDown.IsEnabled = !enable));
            CommandGB.Dispatcher.Invoke(new Action(() => CommandGB.IsEnabled = enable));
            ConnectOptionsGrid.Dispatcher.Invoke(new Action(() => ConnectOptionsGrid.IsEnabled = !enable));
        }
        #endregion

        #region Callbacks
        /// <summary>
        /// Callback for when the client succesfully connects. Also sends credentials
        /// </summary>
        /// <param name="ar"></param>
        private void ConnectCallback(IAsyncResult ar)
        {
            try
            {
                byte[] sendBuffer = Encoding.ASCII.GetBytes(username + "," + password);
                _ClientSocket.Send(sendBuffer, 0, sendBuffer.Length, SocketFlags.None);
                _ClientSocket.EndConnect(ar);
                EnableClientControls(true);
                _ClientSocket.BeginReceive(_Data, 0, _Data.Length, SocketFlags.None, ReceiveCallback, _ClientSocket);
            }
            catch (SocketException ex)
            {
                ClientLog("ConnectCallback " + ex.Message);
            }
            catch (ObjectDisposedException ex)
            {
                ClientLog("ConnectCallback " + ex.Message);
            }
        }

        /// <summary>
        /// Callback for when something is received from the server. Also handles commands
        /// </summary>
        /// <param name="ar"></param>
        private void ReceiveCallback(IAsyncResult ar)
        {
            try
            {
                int received = _ClientSocket.EndReceive(ar);

                if (received == 0)
                {
                    if (_ClientSocket.Connected)
                    {
                        _ClientSocket.Shutdown(SocketShutdown.Both);
                    }
                    _ClientSocket.Close();
                    EnableClientControls(false);
                    ClientLog("Server connection lost");
                    return;
                }

                byte[] receiveBuffer = new byte[received];
                Array.Copy(_Data, receiveBuffer, received);
                string text = Encoding.ASCII.GetString(receiveBuffer);

                ClientLog($@"Received from server: {text}");

                if (text == "Server shutting down" || text == "Invalid username or password, refusing connection")
                {
                    _ClientSocket.Shutdown(SocketShutdown.Both);
                    _ClientSocket.Close();
                    EnableClientControls(false);
                    return;
                }

                _ClientSocket.BeginReceive(_Data, 0, _Data.Length, SocketFlags.None, ReceiveCallback, _ClientSocket);
            }
            catch (SocketException ex)
            {
                ClientLog("ReceiveCallback " + ex.Message);
                //Socket exception when server dies while connected
                _ClientSocket.Shutdown(SocketShutdown.Both);
                _ClientSocket.Close();
                EnableClientControls(false);
            }
            catch (ObjectDisposedException ex)
            {
                ClientLog("ReceiveCallback " + ex.Message);
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
                _ClientSocket.EndSend(ar);
            }
            catch (SocketException ex)
            {
                ClientLog("SendCallback " + ex.Message);
            }
            catch (ObjectDisposedException ex)
            {
                ClientLog("SendCallback " + ex.Message);
            }
        }
        #endregion
    }
}
