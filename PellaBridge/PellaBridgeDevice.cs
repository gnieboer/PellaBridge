using Microsoft.AspNetCore.SignalR;
using Rssdp;

namespace PellaBridge
{
    public class PellaBridgeDevice
    {
        private SsdpEmbeddedDevice _ssdpDevice;
        private int _deviceStatusCode;
        private int _batteryStatus;
        public PellaBridgeDevice(int _id, int _deviceTypeCode)
        {
            DeviceTypeCode = _deviceTypeCode;
            Id = _id;
            _ssdpDevice = new SsdpEmbeddedDevice()
            {
                DeviceTypeNamespace = "SmartthingsCommunity",
                DeviceType = this.DeviceType.Replace(" ", ""),
                FriendlyName = this.DeviceType,
                Manufacturer = "GCN Development",
                ModelName = this.ModelName,
                ModelNumber = Constants.VERSION,
                Uuid = "smartthings-gcndevelopment-" + this.DeviceType.Replace(" ", "").ToLower() + "-" + this.Id
            };
            _ssdpDevice.CustomProperties.Add(new SsdpDeviceProperty()
            {
                Name = "Battery",
                Value = this.BatteryStatus.ToString()
            });
            _ssdpDevice.CustomProperties.Add(new SsdpDeviceProperty()
            {
                Name = "Tampered",
                Value = this.DeviceTampered.ToString()
            });
            _ssdpDevice.CustomProperties.Add(new SsdpDeviceProperty()
            {
                Name = "Status",
                Value = this.DeviceStatus
            }); 
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
        public string ModelName
        {
            get
            {
                return DeviceTypeCode switch
                {
                    0x01 => "PellaDoorSensor.v1",
                    0x03 => "PellaDoorSensor.v1",
                    0x0D => "PellaDoorLock.v1",
                    0x13 => "PellaDoorSensor.v1",
                    _ => "Unknown",
                };
            }
        }

        public SsdpEmbeddedDevice SSDPDevice {
            get {
                return _ssdpDevice;
            }
        }

        public int BatteryStatus
        {
            get
            {
                return _batteryStatus;
            }
            set
            {
                _batteryStatus = value;
                _ssdpDevice.CustomProperties["Battery"].Value = this.BatteryStatus.ToString();
            }
        }

        public int DeviceStatusCode { 
            get 
            {
                return _deviceStatusCode;   
            }
            set 
            {
                _deviceStatusCode = value;
                _ssdpDevice.CustomProperties["Status"].Value = this.DeviceStatus;
                _ssdpDevice.CustomProperties["Tampered"].Value = this.DeviceTampered.ToString();
            }
        }

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