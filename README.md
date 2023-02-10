## Pella Insynctive SmartThings Integration

Version: 2.0.0

This a project to bring integration of the Pella Insynctive system into SmartThings.

The ZWave capability of the bridge is currently not compatible with the ST Hub, at least
as far as I've been able to accomplish.  The bridge can be paired, but the devices themselves
which should "pass through" the bridge to the hub can not.

Instead, this project uses the **wired** interface to accomplish the same objective.  It is not a simple setup, however.
The bridge uses Telnet on port 23 to accept a single connection and send/receive commands.  ST cannot
connect directly to a TCP socket, so this integration requires a REST proxy Docker container to accept calls over HTTP
and convert them to raw TCP and vice versa.

If you are not familiar with docker containers and using PuTTY to connect to devices, this integration might be a challenge.

There is a "tamper" warning if the cover of the door sensors is removed. 
The Up and Down commands for the shades will send them to the "user programmed" open and close positions.  The full set of commands
is in the device handler code, and could be implemented if the user wants something different, but I kept the DH interface simpler based on my preception of real user needs.

LIMITATIONS/ISSUES: 
1. Pella Blind/Shades have not yet been tested with a real shade, it "should work", but could likely be improved.
2. Pella Blinds have a tilt command which is not yet implemented in the device handler (0x68 and 0x69), but will be sent by the proxy.
3. I received extraneous battery level "4%" / "5%" readings which eventually disappear. These appear to be coming from the Pella Bridge itself.
4. I have little time to provide any kind to troubleshooting / technical support for this image.  Pull requests are welcome.

## Installation

### Step 1

Ensure the bridge is not paired to your Zwave hub.  A factory reset is not sufficient

**TEST**: When the bridge is unplugged/replugged, the blue light should flash after the green light comes on

### Step 2

Pair the bridge with the Pella Devices in the "No Home Automation" mode use the instructions for the Pella Bridge product guide
and the product guides for each of the compatible products.
Note that if you have previously tried to pair the bridge to your hub, you will need to specifically unpair the device, a factory reset does not accomplish this.
Range is not as critical for pairing as it is for zwave, the Pella devices use 433MHz and have better range (but no mesh network), so in a normal sized house bridge placement should not be a major issue.

**TEST**: When the door/lock is toggled, the bridge should chime (assume you haven't specifically turned the chime off with the dip switches)

### Step 3

Change the IP address to the bridge.
The default IP is 192.168.100.121.  Plus bridge into your LAN, and use Putty (NOT windows telnet) to telnet into the device.  You should see "Pella Insynctive Bridge" as a banner on connect.
Enter the following command:

```
!BRIDGESETIP,$192.168.000.002
```

Obviously use your preferred LAN IP address.  Note the leading zeros.  This IP will persist after a factory reset.  

**TEST**: Reset device and connect to it via Putty.  Enter the following command to check the number of paired devices matches:
```
?POINTCOUNT
```
The number returned should equal the number of devices paired in step 2.  Disconnect from the bridge.

### Step 4

Install Proxy Container
The proxy is a .NET Core Linux container, and requires two environment variables to be set during container creation:
HUB_IP_ADDRESS : The IP address of the smartthings hub
BRIDGE_IP_ADDRESS : The IP address set in the previous step
Ensure the addresses will both be reachable from the proxy.  They need not be on the same subnet if you have a segmented LAN, but DO NOT
expose the address to the Internet as the bridge has no security (and therefore the proxy does not either).
The proxy connects to the bridge on Port 23, connects to the hub on a dynamic port, and receives from the hub on the port of your choice (32781 by default command below), so those ports need to be open
The proxy advertises via SSDP, and the Edge Driver will listen and discover the IP and port.  However, if you are on different subnets, you will likely have to manually configure the driver.
If your container host uses multiple IP addresses, you make need to modify the default environment variable ASPNETCORE_URLS to specify a specific IP to advertise over SSDP.
The hub driver in the Edge environment will listen on a random port.  It 
Use the following to pull the image from Docker Hub and get it running on the server of your choice, or the equivalent in your favorite Docker GUI.
```
docker pull gcndevelopment/pellabridge
docker run --name pellabridge --env "HUB_IP_ADDRESS=192.168.0.100" --env "BRIDGE_IP_ADDRESS=192.168.0.101" -p 32781:80 gcndevelopment/pellabridge
```
The proxy will immediately connect, so ensure you have disconnected from any manual connections you have made.  The container *should* gracefully handle TCP connection issues, including bridge power offs/disconnects.  (It will complain in the console window, but should eventually re-connect). 
It will keep the connection alive with a 4 minute ping, which is therefore the longest window in which a push notification from the bridge might be missed.

**TEST**: Go to a browser and check:

http://your.proxy.ip:32781/api/PellaBridge [Expected response, json "Hello", "World"]

http://your.proxy.ip:32781/api/PellaBridge/status [Expected response, {"version":"A58","mac":"00:XX:XX:XX:XX:XX","connectTime":"2020-06-06T20:19:38.2991876+00:00","ip":"192.168.0.101"} or similar]

Watch the console output of the container to see the data being transmitted as expected for the second command.

### Step 5

Install the Edge Driver using the following channel invite:

https://bestow-regional.api.smartthings.com/invite/3X21pmo5NRMR

Run "Add Devices".  If SSDP was successful, all your Pella Devices should appear shortly.  Skip to Step 7

**TEST**: You should at a minimum have added a bridge device

### Step 6 [Manual IP Configuration]

Configure the Bridge in the app.  Enter the proxy's IP address (NOT the bridge) and port, and the enter "Next".  The app will query the proxy and get information all the devices and install them.
Once you see the number of devices installed, click "Save"
The device will have preferences populated.  You should not have a need to change them unless you move the hub or reprogram devices (though it may be simpler to just delete them all and re-configure the smartapp)
The bridge itself can be ignored since it really has no function beyond enabling the connections.

**TEST** Observe that the number of installed devices matched expectations.

### Step 7

Devices of the same type will all have the same name, you will have to figure out which is which and rename as appropriate.  

**TEST** You are now complete... ensure that the device are present as expected, and in the expected state.  Ensure that data is being pushed by opening a door etc manually and observing the status change.

### TROUBLESHOOTING

The REST API is a simple one, entirely GET commands, so straightforward to access directly and bypass ST.

\{id} = The Pella Device ID.  Numbers starting a 1, see device preferences for each device's code

- GET: api/PellaBridge  (Hello World)
- GET: api/PellaBridge/status  (Gets bridge status as JSON)
- GET: api/PellaBridge/enumerate (get all devices and their current status)
- GET: api/PellaBridge/battery/{id} (gets battery status as number)
- GET: api/PellaBridge/devicestatus/{id} (gets device status as Pella code, see dev handler code for lookups)
- GET: api/PellaBridge/devicestatusstring/{id} (device status as string)
- GET: api/PellaBridge/setshade/{id}/{value} (send shade command, 0-100 is % open, >100 are special commands, see DH code)
- GET: api/PellaBridge/pushdevice/{id} (send a fake push notification to the ST hub, used only for help troubleshoot)
- GET: api/PellaBridge/registerport/{port} (sets what port the proxy should use when calling back to the hub.  Also displays current status)

A combination of container console logs and ST logs should be plenty to identify where a connection might be failing.  Use the last API call in the list simulate opening the door/lock.
If pushes are failing, restart the container and see if it fixes it.  If that is required often, please try to capture errors from the console and post them.

I'd recommend starting from the bridge and working from there to the Hub.  Examing the container console to be sure the calls are being received and responded to by the bridge, then manually engage the API and confirm that the container is returning expected JSON, then finally ensure ST configuration is correct.





