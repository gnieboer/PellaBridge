using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

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
    }
}
