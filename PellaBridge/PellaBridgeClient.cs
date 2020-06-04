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
    /// High level interface class for Pella Bridge
    /// </summary>
    public class PellaBridgeClient
    {
        private PellaBridgeTCPClient tcpClient;
        private BridgeInfo bridgeInfo;
        private List<PellaBridgeDevice> devices = new List<PellaBridgeDevice>();

        private IPAddress hubIP;

        // default timeout to wait for a response from the bridge
        private const int timeout = 500;

        // Events indicating returns of various types of responses
        private readonly AutoResetEvent evtHello = new AutoResetEvent(false);
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
            try
            {
                lastCommand = "BRIDGEINFO";
                tcpClient.SendCommand("?BRIDGEINFO");
            }
            catch (Exception)
            {

                throw;
            }
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

            try
            {
                lastCommand = "POINTCOUNT";
                tcpClient.SendCommand("?POINTCOUNT");
            }
            catch (Exception)
            {
                throw;
            }
            if (evtPointCount.WaitOne(timeout))
            {
                for (int i = 1; i <= lastPointCount; i++)
                {
                    lastID = i;
                    lastCommand = "POINTDEVICE";
                    tcpClient.SendCommand($"?POINTDEVICE-{i:000}");
                    if (evtPointDevice.WaitOne(timeout))
                    {
                        lastDevice.batteryStatus = this.GetBatteryStatus(i);
                        lastDevice.deviceStatusCode = this.GetDeviceStatus(i);
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
            try
            {
                lastCommand = "POINTSTATUS";
                tcpClient.SendCommand($"?POINTSTATUS-{id:000}");
            }
            catch (Exception)
            {

                throw;
            }
            if (evtDeviceStatus.WaitOne(timeout))
            {
                return lastDeviceStatus;
            }
            else
            {
                throw new TimeoutException("Request timed out");
            }
        }

        internal int GetBatteryStatus(int id)
        {
            try
            {
                lastCommand = "POINTBATTERYGET";
                tcpClient.SendCommand($"?POINTBATTERYGET-{id:000}");
            }
            catch (Exception)
            {

                throw;
            }
            if (evtBatteryStatus.WaitOne(timeout))
            {
                return lastBatteryStatus;
            }
            else
            {
                throw new TimeoutException("Request timed out");
            }
        }

        private void Listener()
        {
            string message;
            Regex BridgeInfoRegex = new Regex(@"Version: (\w*), MAC: ([\w:]*)");
            Regex HelloRegex = new Regex(@"Insynctive Telnet Server");
            Regex StatusChangeRegex = new Regex(@"POINTSTATUS-([0-9]{3}),\$([0-9A-F][0-9A-F])");
            Regex BatteryChangeRegex = new Regex(@"POINTBATTERYGET-([0-9]{3}),\$([0-9A-F][0-9A-F])");
            Regex PointCountRegex = new Regex(@"[0-9]{3}");
            Regex NumericRegex = new Regex(@"\$([0-9A-F][0-9A-F])");
            do
            {
                tcpClient.messageReceived.WaitOne();
                if (tcpClient.messageQueue.TryDequeue(out message))
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
                                IP = tcpClient.bridgeIPAddress.ToString()
                            };
                            lastBridgeInfo = bi;
                            evtBridgeStatus.Set();
                            break;
                        case string msg when StatusChangeRegex.IsMatch(msg):
                            m = StatusChangeRegex.Match(msg);
                            deviceID = Int32.Parse(m.Groups[1].Value);
                            newDeviceStatusCode = Convert.ToInt32(m.Groups[2].Value, 16);
                            device = devices.Find(x => x.id == deviceID);
                            UpdateBattery(device, newDeviceStatusCode);
                            break;
                        case string msg when BatteryChangeRegex.IsMatch(msg):
                            m = StatusChangeRegex.Match(msg);
                            deviceID = Int32.Parse(m.Groups[1].Value);
                            newBatteryStatus = Convert.ToInt32(m.Groups[2].Value, 16);
                            device = devices.Find(x => x.id == deviceID);
                            UpdateDeviceStatus(device, newBatteryStatus);
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
            Trace.WriteLine($"Sending updated battery level to hub on device ID {device.id} to {newBatteryStatus}%");
            device.batteryStatus = newBatteryStatus;
            SendUpdateToHub(device);
        }

        private void UpdateDeviceStatus(PellaBridgeDevice device, int newDeviceStatusCode)
        {
            Trace.WriteLine($"Sending updated status to hub on device ID {device.id} to {newDeviceStatusCode}");
            device.deviceStatusCode = newDeviceStatusCode;
            SendUpdateToHub(device);
        }
        private void SendUpdateToHub(PellaBridgeDevice device)
        {
            string jsonOutput = JsonSerializer.Serialize(device);
            using (var client = new HttpClient())
            {
                UriBuilder b = new UriBuilder("http", hubIP.ToString(), 39500);
                client.PostAsync(
                    b.ToString(),
                     new StringContent(jsonOutput, Encoding.UTF8, "application/json"));
            }
        }
    }
}