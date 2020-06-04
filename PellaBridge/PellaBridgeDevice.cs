using Microsoft.AspNetCore.SignalR;

namespace PellaBridge
{
    public class PellaBridgeDevice
    {
        public PellaBridgeDevice(int _id, int _deviceTypeCode)
        {
            deviceTypeCode = _deviceTypeCode;
            id = _id;
        }

        public int id { get; private set; }
        public int deviceTypeCode { get; private set; }

        public string deviceType { 
            get {
                switch (deviceTypeCode)
                {
                    case 0x01:
                        return "Pella Door";
                    case 0x03:
                        return "Pella Garage Door";
                    case 0x0D:
                        return "Pella Door Lock";
                    case 0x13:
                        return "Pella Blind";
                    default:
                        return "Unknown";
                }
            }
        }

        public int batteryStatus { get; set; }

        public int deviceStatusCode { get; set; }

        public string deviceStatus
        {
            get
            {
                if (deviceTypeCode == 0x0D)
                {
                    switch (deviceStatusCode)
                    {
                        case 0x00:
                        case 0x04:
                            return "Locked";
                        case 0x01:
                        case 0x02:
                        case 0x06:
                        case 0x05:
                            return "Unlocked";
                        default:
                            return "Unknown";
                    }
                }
                else if (deviceTypeCode == 0x13)
                {
                    return $"{deviceStatusCode}%";
                }
                else
                {
                    switch (deviceStatusCode)
                    {
                        case 0x00:
                        case 0x02:
                        case 0x04:
                        case 0x06:
                            return "Closed";
                        case 0x01:
                        case 0x05:
                            return "Open";
                        default:
                            return "Unknown";
                    }
                }
            }
        }

        public bool deviceTampered
        {
            get
            {
                if (deviceTypeCode != 0x13)
                {
                    switch (deviceStatusCode)
                    {
                        case 0x04:
                        case 0x05:
                        case 0x06:
                            return true;
                        default:
                            return false;
                    }
                }
                else
                {
                    // Shades don't have a tamper status
                    return false;
                }
            }
        }
    }

    public class PellaSensor : PellaBridgeDevice
    {
        public PellaSensor(int _id, int _deviceTypeCode) : base(_id, _deviceTypeCode) { }


    }
}