using Microsoft.AspNetCore.SignalR;

namespace PellaBridge
{
    public class PellaBridgeDevice
    {
        public PellaBridgeDevice(int _id, int _deviceTypeCode)
        {
            DeviceTypeCode = _deviceTypeCode;
            Id = _id;
        }

        public int Id { get; private set; }
        public int DeviceTypeCode { get; private set; }

        public string DeviceType { 
            get {
                return DeviceTypeCode switch
                {
                    0x01 => "Pella Door",
                    0x03 => "Pella Garage Door",
                    0x0D => "Pella Door Lock",
                    0x13 => "Pella Blind",
                    _ => "Unknown",
                };
            }
        }

        public int BatteryStatus { get; set; }

        public int DeviceStatusCode { get; set; }

        public string DeviceStatus
        {
            get
            {
                if (DeviceTypeCode == 0x0D)
                {
                    switch (DeviceStatusCode)
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
                else if (DeviceTypeCode == 0x13)
                {
                    return $"{DeviceStatusCode}%";
                }
                else
                {
                    switch (DeviceStatusCode)
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

        public bool DeviceTampered
        {
            get
            {
                if (DeviceTypeCode != 0x13)
                {
                    switch (DeviceStatusCode)
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