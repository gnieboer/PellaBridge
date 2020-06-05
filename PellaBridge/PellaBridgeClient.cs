using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Threading;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;


namespace PellaBridge
{
    /// <summary>
    /// High level interface class for Pella Bridge.
    /// Network connections and TCP level details are handled by PellaBridgeTCPClient, to include handling socket IO exceptions and automatically reconnecting.
    /// Application level formatting and response handling are handled here.  The only exceptions handled here are Timeouts.
    /// </summary>
    public class PellaBridgeClient
    {
        private readonly PellaBridgeTCPClient tcpClient;
        private BridgeInfo bridgeInfo;
        private List<PellaBridgeDevice> devices = new List<PellaBridgeDevice>();

        private readonly IPAddress hubIP;

        // default timeout to wait for a response from the bridge
        private const int timeout = 2000;

        // Events indicating returns of various types of responses
        private readonly AutoResetEvent evtBridgeStatus = new AutoResetEvent(false);
        private readonly AutoResetEvent evtPointCount = new AutoResetEvent(false);
        private readonly AutoResetEvent evtPointDevice = new AutoResetEvent(false);
        private readonly AutoResetEvent evtDeviceStatus = new AutoResetEvent(false);
        private readonly AutoResetEvent evtBatteryStatus = new AutoResetEvent(false);

        // Variables read from responses
        private BridgeInfo lastBridgeInfo;
        private DateTime lastHello;

        private int lastPointCount;
        private PellaBridgeDevice lastDevice;
        private int lastBatteryStatus;
        private int lastDeviceStatus;

        // Variables set by commands
        private int lastID;
        private string lastCommand;

        public PellaBridgeClient()
        {
            tcpClient = new PellaBridgeTCPClient();
            bridgeInfo = new BridgeInfo();
            hubIP = IPAddress.Parse(Environment.GetEnvironmentVariable("HUB_IP_ADDRESS"));
            System.Threading.Thread listenerThread = new System.Threading.Thread(Listener);
            listenerThread.Start();
            tcpClient.Init();
            tcpClient.Connect();
        }

        public BridgeInfo GetBridgeInfo()
        {
            lastCommand = "BRIDGEINFO";
            tcpClient.SendCommand("?BRIDGEINFO");

            if (evtBridgeStatus.WaitOne(timeout))
            {
                bridgeInfo = lastBridgeInfo;
                return bridgeInfo;
            } else
            {
                throw new TimeoutException("Request timed out");
            }
        }

        /// <summary>
        /// Retrieves number of devices, then enumerates each to get current type, then current status
        /// </summary>
        /// <returns>list of devices discovered and current status</returns>
        internal IEnumerable<PellaBridgeDevice> EnumerateDevices()
        {
            List<PellaBridgeDevice> _devices = new List<PellaBridgeDevice>();

            lastCommand = "POINTCOUNT";
            tcpClient.SendCommand("?POINTCOUNT");

            if (evtPointCount.WaitOne(timeout))
            {
                for (int i = 1; i <= lastPointCount; i++)
                {
                    lastID = i;
                    lastCommand = "POINTDEVICE";
                    tcpClient.SendCommand($"?POINTDEVICE-{i:000}");
                    if (evtPointDevice.WaitOne(timeout))
                    {
                        lastDevice.BatteryStatus = this.GetBatteryStatus(i);
                        lastDevice.DeviceStatusCode = this.GetDeviceStatus(i);
                        _devices.Add(lastDevice);
                    }
                    else
                    {
                        throw new TimeoutException("Request timed out");
                    }
                }
                devices = _devices;
                return devices;
            }
            else
            {
                throw new TimeoutException("Request timed out");
            }
        }

        internal int GetDeviceStatus(int id)
        {
            lastCommand = "POINTSTATUS";
            tcpClient.SendCommand($"?POINTSTATUS-{id:000}");

            if (evtDeviceStatus.WaitOne(timeout))
            {
                return lastDeviceStatus;
            }
            else
            {
                throw new TimeoutException("Request timed out");
            }
        }

        internal string GetDeviceStatusString(int id)
        {
            lastCommand = "POINTSTATUS";
            tcpClient.SendCommand($"?POINTSTATUS-{id:000}");

            if (evtDeviceStatus.WaitOne(timeout))
            {
                PellaBridgeDevice d = devices.Find(x => x.Id == id);
                if (d is null)
                {
                    throw new InvalidOperationException("Device ID not found");
                } else
                {
                    d.DeviceStatusCode = lastDeviceStatus;
                }
                return d.DeviceStatus;
            }
            else
            {
                throw new TimeoutException("Request timed out");
            }
        }

        internal int GetBatteryStatus(int id)
        {
            lastCommand = "POINTBATTERYGET";
            tcpClient.SendCommand($"?POINTBATTERYGET-{id:000}");

            if (evtBatteryStatus.WaitOne(timeout))
            {
                return lastBatteryStatus;
            }
            else
            {
                throw new TimeoutException("Request timed out");
            }
        }

        internal string[] SetShade(int id, int value)
        {
            PellaBridgeDevice device = devices.Find(x => x.Id == id);
            if (device is null)
            {
                return new string[] { "Error", "Device ID not found" };
            }
            if (device.DeviceTypeCode != 0x13)
            {
                return new string[] { "Error", "Command sent to incompatible device" };
            }
            if (value < 0 || value > 106)
            {
                return new string[] { "Error", "Command sent an invalid value, should be 0x00-0x6A" };
            }
            tcpClient.SendCommand($"POINTSET-{id:000},${value:X2}");
            return new string[] { "Success", "" };
        }

        private void Listener()
        {
            Regex BridgeInfoRegex = new Regex(@"Version: (\w*), MAC: ([\w:]*)");
            Regex HelloRegex = new Regex(@"Insynctive Telnet Server");
            Regex StatusChangeRegex = new Regex(@"POINTSTATUS-([0-9]{3}),\$([0-9A-F][0-9A-F])");
            Regex BatteryChangeRegex = new Regex(@"POINTBATTERYGET-([0-9]{3}),\$([0-9A-F][0-9A-F])");
            Regex PointCountRegex = new Regex(@"[0-9]{3}");
            Regex NumericRegex = new Regex(@"\$([0-9A-F][0-9A-F])");
            do
            {
                // Don't wait forever, in case the TCPClient has thrown a socket error elsewhere and been replaced with a 
                // new connection.  
                tcpClient.messageReceived.WaitOne(timeout / 2);
                // While unlikely, it's possible multiple messages could be waiting after a single event, so loop until all are processed.
                // Note that a storm of messages is likely to cause issues, as we only track the last command received
                // So responses may end up getting matching with the wrong request.  But that it unavoidable and 
                // will likely never happen in reality.  Instead what is more likely is the device pushes an update
                // while we are sending a command or refresh, and that should work fine.
                while (tcpClient.messageQueue.TryDequeue(out string message))
                {
                    Match m;
                    int deviceID;
                    int newDeviceStatusCode;
                    int newBatteryStatus;
                    PellaBridgeDevice device;

                    switch (message)
                    {
                        case string msg when HelloRegex.IsMatch(msg):
                            lastHello = DateTime.Now;
                            break;
                        case string msg when BridgeInfoRegex.IsMatch(msg):
                            m = BridgeInfoRegex.Match(msg);
                            BridgeInfo bi = new BridgeInfo
                            {
                                Version = m.Groups[1].Value,
                                MAC = m.Groups[2].Value,
                                connectTime = lastHello,
                                IP = tcpClient.BridgeIPAddress.ToString()
                            };
                            lastBridgeInfo = bi;
                            evtBridgeStatus.Set();
                            break;
                        case string msg when StatusChangeRegex.IsMatch(msg):
                            m = StatusChangeRegex.Match(msg);
                            deviceID = Int32.Parse(m.Groups[1].Value);
                            newDeviceStatusCode = Convert.ToInt32(m.Groups[2].Value, 16);
                            device = devices.Find(x => x.Id == deviceID);
                            if (device is null) break;
                            UpdateDeviceStatus(device, newDeviceStatusCode);
                            break;
                        case string msg when BatteryChangeRegex.IsMatch(msg):
                            m = StatusChangeRegex.Match(msg);
                            deviceID = Int32.Parse(m.Groups[1].Value);
                            newBatteryStatus = Convert.ToInt32(m.Groups[2].Value, 16);
                            device = devices.Find(x => x.Id == deviceID);
                            if (device is null) break;
                            UpdateBattery(device, newBatteryStatus);
                            break;
                        case string msg when PointCountRegex.IsMatch(msg):
                            m = PointCountRegex.Match(msg);
                            lastPointCount = Int32.Parse(m.Groups[0].Value);
                            evtPointCount.Set();
                            break;
                        case string msg when NumericRegex.IsMatch(msg) && lastCommand == "POINTDEVICE":
                            m = NumericRegex.Match(msg);
                            lastDevice = new PellaBridgeDevice(lastID, Convert.ToInt32(m.Groups[1].Value,16));
                            evtPointDevice.Set();
                            break;
                        case string msg when NumericRegex.IsMatch(msg) && lastCommand == "POINTBATTERYGET":
                            m = NumericRegex.Match(msg);
                            lastBatteryStatus = Convert.ToInt32(m.Groups[1].Value, 16);
                            evtBatteryStatus.Set();
                            break;
                        case string msg when NumericRegex.IsMatch(msg) && lastCommand == "POINTSTATUS":
                            m = NumericRegex.Match(msg);
                            lastDeviceStatus = Convert.ToInt32(m.Groups[1].Value, 16);
                            evtDeviceStatus.Set();
                            break;
                    }
                } 
            } while (true);
        }

        private void UpdateBattery(PellaBridgeDevice device, int newBatteryStatus)
        {
            Trace.WriteLine($"Sending updated battery level to hub on device ID {device.Id} to {newBatteryStatus}%");
            device.BatteryStatus = newBatteryStatus;
            SendUpdateToHub(device);
        }

        private void UpdateDeviceStatus(PellaBridgeDevice device, int newDeviceStatusCode)
        {
            Trace.WriteLine($"Sending updated status to hub on device ID {device.Id} to {newDeviceStatusCode}");
            device.DeviceStatusCode = newDeviceStatusCode;
            SendUpdateToHub(device);
        }
        private void SendUpdateToHub(PellaBridgeDevice device)
        {
            string jsonOutput = JsonSerializer.Serialize(device);
            using var client = new HttpClient();
            UriBuilder b = new UriBuilder("http", hubIP.ToString(), 39500);
            client.PostAsync(
                b.ToString(),
                 new StringContent(jsonOutput, Encoding.UTF8, "application/json"));
        }
    }
}