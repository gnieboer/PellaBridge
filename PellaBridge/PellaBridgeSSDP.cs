using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;
using Rssdp;
using System.Net;

namespace PellaBridge
{
    public class PellaBridgeSSDP
    {
        private SsdpDevicePublisher _Publisher;
        private SsdpRootDevice _deviceDefinition;
        private Uri _uri;
        public PellaBridgeSSDP()
        {
            //Note, you can use deviceDefinition.ToDescriptionDocumentText() to retrieve the data to 
            //return from the Location end point, you just need to get that data to your service
            //implementation somehow. Depends on how you've implemented your service.

            // As this is a sample, we are only setting the minimum required properties.
            Trace.WriteLine("IPs: " + string.Join(",", new List<IPAddress>(Dns.GetHostAddresses(Dns.GetHostName()))));
            // ASPNETCORE_URLS is a required part of the ASP.NET Core implementation and should always be present.
            // Normally it will listen on all IP's and have a "+" for the IP
            string myEnvIP = Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
            string name = Dns.GetHostName(); // get container id
            string localip = Dns.GetHostEntry(name).AddressList.FirstOrDefault(x => x.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork).ToString();

            if (myEnvIP != null)
            {
                if (myEnvIP.Contains("*") || myEnvIP.Contains("+"))
                {
                    myEnvIP = myEnvIP.Replace("+", localip).Replace("*", localip);
                }
                try
                {
                    _uri = new Uri(myEnvIP);
                }
                catch (FormatException ex)
                {
                    Console.WriteLine("Env Variable ASPNETCORE_URLS in invalid format: {0}", myEnvIP);
                    throw ex;
                }
            } else
            {
                Console.WriteLine("Env Variable ASPNETCORE_URLS is not set");
                throw new ApplicationException();
            }
            Trace.WriteLine("Selected endpoint: " + _uri.ToString());

            _deviceDefinition = new SsdpRootDevice()
            {
                CacheLifetime = TimeSpan.FromMinutes(30), //How long SSDP clients can cache this info.
                Location = new Uri(_uri.ToString() + "api/PellaBridge/description"), // Must point to the URL that serves your devices UPnP description document. 
                DeviceTypeNamespace = "SmartthingsCommunity",
                DeviceType = "PellaBridge",
                FriendlyName = "Smartthings Pella Bridge",
                Manufacturer = "GCN Development",
                ManufacturerUrl = new Uri("https://gcndevelopment.com"),
                ModelUrl = new Uri("https://github.com/gnieboer/PellaBridge"),
                PresentationUrl = new Uri(_uri.ToString() + "api/PellaBridge"), // directs to Hello World page.  There is nothing to control really.  This could be replaced with an API description / WSDL webpage if we made one
                ModelName = "PellaBridge.v1",
                ModelNumber = Constants.VERSION,
                SerialNumber = System.Environment.MachineName, // Container GUID
                Uuid = "smartthings-gcndevelopment-pellabridge"
            };

            _Publisher = new SsdpDevicePublisher();
            _Publisher.StandardsMode = SsdpStandardsMode.Relaxed;
        }

        public void StartPublishing()
        {
            _Publisher.AddDevice(_deviceDefinition);
            Trace.WriteLine("Publishing SSDP");
        }

        public void StopPublishing()
        {
            _Publisher.RemoveDevice(_deviceDefinition);
        }

        public void UpdateSSDP(IEnumerable<PellaBridgeDevice> _devices)
        {
            foreach (SsdpEmbeddedDevice d in _deviceDefinition.Devices)
            {
                _deviceDefinition.RemoveDevice(d);
            }

            foreach (PellaBridgeDevice d in _devices)
            {
                _deviceDefinition.AddDevice(d.SSDPDevice);
            }
        }

        public string DescribeDevices()
        {
            return _deviceDefinition.ToDescriptionDocument();
        }
    }
}
