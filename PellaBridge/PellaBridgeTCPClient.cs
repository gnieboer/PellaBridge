using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace PellaBridge
{
    /// <summary>
    /// Handles low-level connection with the Pella Bridge over TCP
    /// Has no knowledge of the Pella command structure
    /// </summary>
    public class PellaBridgeTCPClient
        
    {
        private TcpClient tcpClient;
        private NetworkStream netStream;
        /// <summary>
        /// Event consumed by subscriber to indicate data was received and has been queued
        /// </summary>
        public AutoResetEvent messageReceived = new AutoResetEvent(false);
        /// <summary>
        /// Thread-safe queue for subscriber to retrieve messages
        /// </summary>
        public ConcurrentQueue<string> messageQueue = new ConcurrentQueue<string>();

        /// <summary>
        /// This will be set to the default until informed by ST of the correct option or via environment variable in the Docker setup
        /// </summary>
        public IPAddress BridgeIPAddress { get; private set; }
        /// <summary>
        /// Telnet not changeable
        /// </summary>
        public const int port = 23;
        /// <summary>
        /// Default Pella bridge IP address prior to persistent customization using !BRIDGESETIP,$xxx.xxx.xxx.xxx               [Note: need the leading zeros in the address]
        /// </summary>
        public const string defaultIPAddress = "192.168.100.121";


        /// <summary>
        /// Keep Alive setting every "X" seconds
        /// </summary>
        public const int keepAliveTimesec = 180;
        /// <summary>
        /// Manual 'Keep Alive' ping every "X" minutes
        /// </summary>
        public const int pingIntervalmins = 4;
        /// <summary>
        /// When reconnecting, how long to wait between attempts in ms
        /// </summary>
        public const int reconnectWaitTimems = 1000;
        /// <summary>
        /// How long to wait between checks for incoming data in the read buffer.
        /// </summary>
        public const int ReceiveSpinWaitTimems = 50;

        public PellaBridgeTCPClient()
        {
            GetNewTCPClient();
        }

        private void GetNewTCPClient()
        {
            tcpClient?.Close();
            netStream = null;
            tcpClient = new TcpClient();
            tcpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
            tcpClient.Client.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, keepAliveTimesec);
        }

        internal void Init()
        {
            string bridgeEnvIP = Environment.GetEnvironmentVariable("BRIDGE_IP_ADDRESS");

            if (bridgeEnvIP == null)
            {
                BridgeIPAddress = IPAddress.Parse(defaultIPAddress);
            } else
            {
                try
                {
                    BridgeIPAddress = IPAddress.Parse(bridgeEnvIP);
                }
                catch (FormatException)
                {
                    Trace.WriteLine("Env Variable IP Address in invalid format: {0}", bridgeEnvIP);
                }
            }
        }

        internal void Connect()
        {
            System.Threading.Thread listenerThread = new System.Threading.Thread(Listener);
            System.Threading.Thread pingerThread = new System.Threading.Thread(Pinger);
            do
            {
                try
                {
                    if (BridgeIPAddress is null)
                    {
                        throw new InvalidOperationException("Bridge must be initialized before Connect() can be called");
                    }
                    if (tcpClient.Connected)
                    {
                        GetNewTCPClient();
                    }
                    tcpClient.Connect(BridgeIPAddress, port);
                    netStream = tcpClient.GetStream();
                }
                catch (SocketException e)
                {
                    Trace.WriteLine($"SocketException: {e}");
                    GetNewTCPClient();
                    Thread.Sleep(500); 
                }
            } while (!tcpClient.Connected);

            listenerThread.Start();
            pingerThread.Start();
        }

        /// <summary>
        /// Sends the low-level TCP query/command to the bridge
        /// This method must be synchronized as only one thread can access netStream.write() at once.
        /// </summary>
        /// <param name="request">Command string to send to the Pella Bridge</param>
        [MethodImpl(MethodImplOptions.Synchronized)]
        internal void SendCommand(string request)
        {
            byte[] outdata = Encoding.ASCII.GetBytes(request);

            try
            {
                // While we could check if the connection is open and the netStream.CanWrite is true, most errors will happen here and the corrective
                // action is the same, so just blindly try to write.
                netStream.Write(outdata, 0, outdata.Length);
                if (request == "\r\n")
                {
                    Trace.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} Sent CRLF Ping");
                } else
                {
                    Trace.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} S- {request}");
                }
                
            }
            catch (SocketException e)
            {
                Trace.WriteLine($"Socket Error received while transmitting data: {e}");
                Reconnect();
                netStream.Write(outdata, 0, outdata.Length);
                if (request == "\r\n")
                {
                    Trace.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} Sent CRLF Ping");
                }
                else
                {
                    Trace.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} S- {request}");
                }
            }
        }

        /// <summary>
        /// Listener reads incoming data from the Pella Bridge and places it into a FIFO queue
        /// and fires an event to notify upstream that new data has been received for parsing.
        /// This should only be called once, as only one thread can access netStream.Read() at a time.
        /// </summary>
        private void Listener()
        {
            byte[] indata = new byte[128];
            int messagelength;

            do
            {
                if(tcpClient.Connected)
                {
                    // Due to simplicity of implementation, we assume all messages are under the buffer size
                    // So we don't merge multiple retrievals into a single message
                    while (netStream.DataAvailable)
                    {
                        try
                        {
                            messagelength = netStream.Read(indata, 0, indata.Length);
                        }
                        catch (SocketException e)
                        {
                            Trace.WriteLine($"Socket Error received while receiving data: {e}");
                            Reconnect();
                            messagelength = netStream.Read(indata, 0, indata.Length);
                        }
                        if (messagelength > 0)
                        {
                            string message = Encoding.ASCII.GetString(indata, 0, messagelength);
                            Trace.Write($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} R- {message}");  // responses include crlf
                            // in case multiple messages end up in the same buffer read, they should be sep'd by a crlf
                            foreach (string msg in message.Split("\r\n"))
                            {
                                messageQueue.Enqueue(msg);
                            }
                            messageReceived.Set();
                        }
                        // A faster spin when there might be a follow-on command coming
                        // Generally commands seem to take about 7ms to process, so we'll wait 10ms
                        // to ensure we catch the next command on this spin.
                        Thread.Sleep(10);
                    } 
                    Thread.Sleep(ReceiveSpinWaitTimems);
                } else
                {
                    Thread.Sleep(ReceiveSpinWaitTimems);
                }
            } while (true);
        }

        /// <summary>
        /// KeepAlive should in theory keep the connection open, and sending commands will automatically re-open a pipe
        /// However, if the connection dies, we will not receive notifications until some action re-established the pipe
        /// So we need to manually ping every so often as KeepAlive has not proven sufficiently reliable.
        /// </summary>
        private void Pinger()
        {
            do
            {
                Thread.Sleep(pingIntervalmins * 1000 * 60);
                SendCommand("\r\n");
            } while (true);
        }

        /// <summary>
        /// This continously tries to reconnect the socket.  It will try forever since the app has no purpose without a connection
        /// Note there is a potential issue remaining here as both SendCommand and Listener might try to call here.
        /// Only one thread can try to reconnect, so the other is going to be queued, so when the first reconnects, the second
        /// is unaware of the success and so will disconnect and reconnect again.
        /// </summary>
        [MethodImpl(MethodImplOptions.Synchronized)]
        internal void Reconnect()
        {
            do
            {
                try
                {
                    tcpClient.Close();
                    tcpClient.Dispose();
                    GetNewTCPClient();
                    tcpClient.Connect(BridgeIPAddress, port);
                    netStream = tcpClient.GetStream();
                    return;
                }
                catch (SocketException e)
                {
                    Trace.WriteLine($"Socket Error received while reconnecting: {e}");
                    Thread.Sleep(reconnectWaitTimems);
                }

            } while (true);
        }
    }
}