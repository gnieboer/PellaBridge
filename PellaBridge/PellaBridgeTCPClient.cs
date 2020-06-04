using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
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
        /// This will be set to the default until informed by ST of the correct option or via environment variable in the Docker setup
        /// </summary>
        public IPAddress bridgeIPAddress { get; private set; }
        /// <summary>
        /// Telnet not changeable
        /// </summary>
        public const int port = 23;
        /// <summary>
        /// Default Pella bridge IP address prior to persistent customization using !BRIDGESETIP,$xxx.xxx.xxx.xxx               [Note: need the leading zeros in the address]
        /// </summary>
        public const string defaultIPAddress = "192.168.100.121";

        public AutoResetEvent messageReceived = new AutoResetEvent(false);

        public ConcurrentQueue<string> messageQueue = new ConcurrentQueue<string>();

        public PellaBridgeTCPClient()
        {
            Busy = true; 
            tcpClient = new TcpClient();
            tcpClient.LingerState.Enabled = false;                          // The default but stated explicitly for clarity
        }

        /// <summary>
        /// The bridge is a simple single connection TCP server, so we will block if the hub sends multiple requests async-ly
        /// </summary>
        public bool Busy { get; private set; }

        internal void Init()
        {
            string bridgeEnvIP = Environment.GetEnvironmentVariable("BRIDGE_IP_ADDRESS");

            if (bridgeEnvIP == null)
            {
                bridgeIPAddress = IPAddress.Parse(defaultIPAddress);
            } else
            {
                try
                {
                    bridgeIPAddress = IPAddress.Parse(bridgeEnvIP);
                }
                catch (FormatException)
                {
                    Trace.WriteLine("Env Variable IP Address in invalid format: {0}", bridgeEnvIP);
                }
            }
        }

        internal void Init(IPAddress _bridgeIPAddress)
        {
            this.bridgeIPAddress = _bridgeIPAddress;
        }

        /// <summary>
        /// Sends the low-level TCP query/command to the bridge
        /// </summary>
        /// <param name="request">Command string to send to the Pella Bridge</param>
        /// <returns>The bridge's response</returns>
        internal int SendCommand(string request)
        {
            if (!tcpClient.Connected)
            {
                Trace.WriteLine("Request received while client disconnected, attempting reconnect");
                if (this.Connect() != 0)
                {
                    return 1;
                }
            }

            byte[] outdata = Encoding.ASCII.GetBytes(request);

            try
            {
                netStream.Write(outdata, 0, outdata.Length);
                Trace.WriteLine($"S- {request}");
            }
            catch (System.IO.IOException e)
            {
                Trace.WriteLine($"IO Error received while transmitting data: {e}");
                return e.HResult;
            }

            return 0;
        }


        internal int Connect(IPAddress _bridgeIPAddress)
        {
            this.Init(_bridgeIPAddress);
            return this.Connect();
        }

        internal int Connect()
        {
            try
            {
                if (tcpClient.Connected)
                {
                    tcpClient.Close();
                    netStream = null;                                   // Ensure we don't keep a reference to a transiently invalidating stream
                    tcpClient = new TcpClient();
                }
                if (bridgeIPAddress is null)
                {
                    throw new InvalidOperationException("Bridge must be initialized before Connect() can be called");
                }
                System.Threading.Thread listenerThread = new System.Threading.Thread(Listener);
                tcpClient.Connect(bridgeIPAddress, port);
                netStream = tcpClient.GetStream();
                listenerThread.Start();
            }
            catch (SocketException e)
            {
                Trace.WriteLine($"SocketException: {e}");
                return e.ErrorCode;
            }
            return 0;
        }

        /// <summary>
        /// Listener reads incoming data from the Pella Bridge and places it into a FIFO queue
        /// and fires an event to notify upstream that new data has been received for parsing.
        /// </summary>
        private void Listener()
        {
            byte[] indata = new byte[256];
            int messagelength = 0;

            do
            {
                if(tcpClient.Connected)
                {
                    // Due to simplicity of implementation, we assume all messages are under the buffer size
                    if (netStream.DataAvailable)
                    {
                        messagelength = netStream.Read(indata, 0, indata.Length);
                        if (messagelength > 0)
                        {
                            string message = Encoding.ASCII.GetString(indata, 0, messagelength);
                            Trace.Write($"R- {message}");  // responses include crlf
                            messageQueue.Enqueue(message);
                            messageReceived.Set();
                        }
                    } else
                    {
                        Thread.Sleep(10);
                    }
                } else
                {
                    Thread.Sleep(10);
                }
            } while (true);
        }
    }
}