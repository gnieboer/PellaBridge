using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace PellaBridge.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PellaBridgeController : ControllerBase
    {
        // GET: api/<PellaBridgeController>
        [HttpGet]
        public IEnumerable<string> Get()
        {
            return new string[] { "Hello", "World" };
        }

        // GET: api/<PellaBridgeController>/status
        [HttpGet]
        [Route("status")]
        public BridgeInfo BridgeStatus()
        {
            return Program.pellaBridgeClient.GetBridgeInfo();
        }

        // GET: api/<PellaBridgeController>/enumerate
        [HttpGet]
        [Route("enumerate")]
        public IEnumerable<PellaBridgeDevice> Enumerate()
        {
            return Program.pellaBridgeClient.EnumerateDevices();
        }

        // GET: api/<PellaBridgeController>/battery/1
        [HttpGet]
        [Route("battery/{id}")]
        public int BatteryStatus(int id)
        {
            return Program.pellaBridgeClient.GetBatteryStatus(id);
        }

        // GET: api/<PellaBridgeController>/devicestatus/1
        [HttpGet]
        [Route("devicestatus/{id}")]
        public int DeviceStatus(int id)
        {
            return Program.pellaBridgeClient.GetDeviceStatus(id);
        }

        // GET: api/<PellaBridgeController>/devicestatusstring/1
        [HttpGet]
        [Route("devicestatusstring/{id}")]
        public string DeviceStatusString(int id)
        {
            return Program.pellaBridgeClient.GetDeviceStatusString(id);
        }

        // GET: api/<PellaBridgeController>/setshade/1/106
        [HttpGet]
        [Route("setshade/{id}/{value}")]
        public IEnumerable<string> SetShade(int id, int value)
        {
            return Program.pellaBridgeClient.SetShade(id, value);
        }

        /// <summary>
        /// A test method to allow the user to manually trigger a push notification from the container to the hub
        /// Obviates the need to go open and close doors just to troubleshoot the push.
        /// Container does not give feedback if the push succeeded on this end
        /// </summary>
        /// <param name="id">Pella id of device to send</param>
        /// <returns>request device</returns>
        // GET: api/<PellaBridgeController>/pushdevice/1
        [HttpGet]
        [Route("pushdevice/{id}")]
        public PellaBridgeDevice PushDevice(int id)
        {
            return Program.pellaBridgeClient.PushDevice(id);
        }

        // <summary>
        // Returns an XML string to be used by SSDP discoverers
        // </summary>
        // <returns>Returns an XML string to be used by SSDP discoverers
        [HttpGet]
        [Route("description")]
        public string GetDeviceDescription()
        {
            string desc;
            desc = Program.pellaBridgeClient.DescribeDevices();
            return desc;
        }

        /// <summary>
        /// In the Edge architecture, each driver is dynamically assigned a port to listen on
        /// This will set what port on the hub the updates should be sent to
        /// </summary>
        /// <param name="port">port that hub driver is listening on</param>
        /// <returns>XML document describing current devices including current status.  Allows a single poll request to update port and get status</returns>
        [HttpGet]
        [Route("registerport/{port}")]
        public string RegisterPort(int port)
        {
            Program.pellaBridgeClient.RegisterPort(port);
            return Program.pellaBridgeClient.DescribeDevices(); ;
        }
    }
}
