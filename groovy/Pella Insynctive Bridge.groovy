/**
 *  Pella Insynctive Bridge
 *
 *  Copyright 2020 Geof Nieboer
 *
 *  Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except
 *  in compliance with the License. You may obtain a copy of the License at:
 *
 *      http://www.apache.org/licenses/LICENSE-2.0
 *
 *  Unless required by applicable law or agreed to in writing, software distributed under the License is distributed
 *  on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License
 *  for the specific language governing permissions and limitations under the License.
 *
 */
definition(
    name: "Pella Insynctive Bridge",
    namespace: "gnieboer",
    author: "Geof Nieboer",
    description: "SmartApp that connects with the Pella Insynctive Bridge and discovers the devices it controls",
    category: "Safety & Security",
    iconUrl: "https://s3.amazonaws.com/smartapp-icons/Convenience/Cat-Convenience.png",
    iconX2Url: "https://s3.amazonaws.com/smartapp-icons/Convenience/Cat-Convenience@2x.png",
    iconX3Url: "https://s3.amazonaws.com/smartapp-icons/Convenience/Cat-Convenience@2x.png",
    singleInstance: true
)

preferences {
    page(name: "searchTargetSelection", title: "Pella Bridge", nextPage: "discoverDevicesPage", content: "setTargetPreferences") {
        section("Bridge") {
            input "searchTarget", "string", title: "IP Address", defaultValue: "192.168.0.122", required: true
            input "searchTargetPort", "string", title: "Port", defaultValue: "32768", required: true
        }
    }
    page(name: "discoverDevicesPage", title: "Pella Insynctive Device Setup", content: "discoverDevices") {}
}

// return host to children devices that ask
def getHost()
{
	def host = "${searchTarget}:${searchTargetPort}"
    
    host
}

def installed() {
	log.debug "Installed with settings: ${settings}"
	state.config = 0
}

def uninstall(){
    log.debug "Removing Pella Devices..."
    childDevices.each {
		try{
			deleteChildDevice(it.deviceNetworkId, true)
		}
		catch (e) {
			log.debug "Error deleting ${it.deviceNetworkId}: ${e}"
		}
	}
}

def updated() {
	log.debug "Updated with settings: ${settings}"
	unsubscribe()
    subscribe(location, null, pushHandler, [filterEvents:false])   
    state.config = 0
}

def pushHandler(evt)
{
    def msg = parseLanMessage(evt.description);
    if (msg.headers && msg.headers["content-type"] && msg.headers["content-type"].contains("application/json")) {
        if (msg && msg.json ) {
        	def device = msg.json
        	if (device.DeviceTypeCode){
                log.debug "fingerprint match"
                def children = getChildDevices()
                def foundChildIndex  
                if (children.size() > 0) {
                    foundChildIndex = children.settings.deviceid.findIndexOf { it == device.Id}
                }
                else {
                    foundChildIndex = -1
                } 
                if (foundChildIndex > -1) {
                    def matchingDevice = children[foundChildIndex]
                    if (matchingDevice) {
                        log.debug "Matching device found: ${matchingDevice}"    
                        log.debug "updating: ${device}"
                        matchingDevice.updateDevice(device)
                        matchingDevice.sendEvent(name: battery, value:50)
                    }
                }
            }
        } else {
        	if (msg.body) {
        		log.warn "received event: ${evt}, malformed json ${msg.body}"
            } else 
            {
            	log.debug "received event: ${evt}, empty json message"
            }
        }
    } else {
    	log.debug "received event: ${evt}, not a json message"
    }
}

def setTargetPreferences ()
{
	state.config = 0
	return dynamicPage(name: "searchTargetSelection", title: "Pella Bridge", nextPage: "discoverDevicesPage", content: "setTargetPreferences") {
        section("Bridge") {
            input "searchTarget", "string", title: "IP Address", required: true
            input "searchTargetPort", "string", title: "Port", required: true
        }
    }
}

def discoverDevices() {

	if (state.config != 1)
    {
    	state.config = 1
        String host = "${searchTarget}:${searchTargetPort}"
        log.debug("Requesting Bridge Status")
        sendHubCommand(new physicalgraph.device.HubAction("""GET /api/PellaBridge/status HTTP/1.1\r\nHOST: $host\r\n\r\n""", physicalgraph.device.Protocol.LAN, host, [callback:bridgeStatusHandler]))
        log.debug("Requesting device list from bridge")
        sendHubCommand(new physicalgraph.device.HubAction("""GET /api/PellaBridge/enumerate HTTP/1.1\r\nHOST: $host\r\n\r\n""", physicalgraph.device.Protocol.LAN, host, [callback: deviceDiscoveryHandler]))

        return dynamicPage(name: "discoverDevicesPage", title: "Device Configuration", nextPage: "", refreshInterval: 3, install: false, uninstall: false) {
            section("Devices") {
                paragraph "Please wait while we discover and install your Pella Devices"
            }
        }
    } else 
    {
    	state.config = 0
        log.debug("Displaying Devices")
        def installeddevices = getChildDevices()
        def doors = 0
        def locks = 0
        def shades = 0
        def garagedoors = 0
        installeddevices.each {
        	switch (it.getTypeName()) {
			case "Pella Door Lock":
            	locks = locks + 1
                break;
            case "Pella Door":
            	doors = doors + 1
                break;
            case "Pella Garage Door":
            	garagedoors = garagedoors + 1
                break;
            case "Pella Blind":
            	shades = shades + 1
                break;
            }
        }
    	return dynamicPage(name: "discoverDevicesPage", title: "Device Configuration", nextPage: "", install: true, uninstall: true) {
            section("Devices") {
                paragraph title: "Pella Door Sensors", "added: ${state.newDevices["Door Sensors"]}   total installed: ${doors}"
                paragraph title: "Pella Lock Sensors",  "added: ${state.newDevices["Lock Sensors"]}   total installed: ${locks}"
                paragraph title: "Pella Shades", "added: ${state.newDevices["Shades"]}   total installed: ${shades}"
                paragraph title: "Pella Garage Door Sensors", "added: ${state.newDevices["Garage Door Sensors"]}   total installed: ${garagedoors}"
            }
        }
    }
}

def bridgeStatusHandler(physicalgraph.device.HubResponse hubResponse)
{
	def json = hubResponse.json
    if (json) {
      log.debug("Bridge Status received: ${json}")
    } else {
      log.debug("Invalid Bridge Status received")
    }
    // dni's should be uppercase with no colons... the bridge provides uppercase but with colons
    state.mac = json.mac.replaceAll("[^A-Z0-9]+","")
    state.version = json.version
    state.connecttime = json.connectTime
    state.ip = json.ip
}

def deviceDiscoveryHandler(physicalgraph.device.HubResponse hubResponse) {
	def json = hubResponse.json
    if (json) {
    	log.debug("device list received: ${json}")
    }
    state.newDevices = [:]
    state.newDevices["Door Sensors"] = 0
    state.newDevices["Lock Sensors"] = 0
    state.newDevices["Garage Door Sensors"] = 0
    state.newDevices["Shades"] = 0
    
    def devices = getChildDevices()
    
    json.each { 
    	def devicejson = it
    	def childid = it.id
        def deviceType = it.deviceType
    	log.debug "finding: ${state.mac + "_" + childid}"
    	def matchingDevice = devices.find({ it.deviceNetworkId == "${state.mac + "_" + childid}" })
        if (matchingDevice) {
        	if (matchingDevice.getTypeName() == deviceType) {
             	// Save type of devices as current stored... update values since we got them
                log.debug("updating device ID ${childid}")
                matchingDevice.state.deviceStatusCode = it.deviceStatusCode
            	matchingDevice.state.batteryStatus = it.batteryStatus
            } else 
            {
            	// Different device than we currently have stored... remove and replace
                log.debug("Remove/Replacing device ID ${childid}, was dev ${matchingDevice.getTypeName()}, is ${deviceType}")
                try
                {
                	deleteChildDevice(matchingDevice.deviceNetworkId)
                    addDevice(devicejson)
                }
                catch (Exception e) {
        			log.error "Unable to delete device with DNI ${matchingDevice.deviceNetworkId}"
    			}
            }
        } else {
        	// No match, add
            log.debug("Adding device ID ${childid}")
            addDevice(it)
        }
    }
    log.debug("Completed enumerating devices from bridge")
}

def addDevice(Object devicejson)
{
	def hub = location.hubs[0]
    if (devicejson.deviceTypeCode == 13) {
    	log.debug "Adding new lock: ${devicejson}"
         state.newDevices["Lock Sensors"] = state.newDevices["Lock Sensors"] + 1
    	addChildDevice("gnieboer", devicejson.deviceType, state.mac + "_" + devicejson.id, hub.getId(), ["preferences":["deviceid": "${devicejson.id}", "apihostip": "${searchTarget}", "apihostport": "${searchTargetPort}"], "state": ["lock": "${devicejson.deviceStatus}", "battery": "${devicejson.batteryStatus}", "tamper": "${devicejson.deviceTampered}"]])
    }
    if (devicejson.deviceTypeCode == 01) {
    	log.debug "Adding new door sensor: ${devicejson}"
        state.newDevices["Door Sensors"] = state.newDevices["Door Sensors"] + 1
    	addChildDevice("gnieboer", devicejson.deviceType, state.mac + "_" + devicejson.id, hub.getId(), ["preferences":["deviceid": "${devicejson.id}", "apihostip": "${searchTarget}", "apihostport": "${searchTargetPort}"], "state": ["contact": "${devicejson.deviceStatus}", "battery": "${devicejson.batteryStatus}", "tamper": "${devicejson.deviceTampered}"]])
    }
    if (devicejson.deviceTypeCode == 03) {
    	log.debug "Adding new garage door sensor: ${devicejson}"
        state.newDevices["Garage Door Sensors"] = state.newDevices["Garage Door Sensors"] + 1
    	addChildDevice("gnieboer", devicejson.deviceType, state.mac + "_" + devicejson.id, hub.getId(), ["preferences":["deviceid": "${devicejson.id}", "apihostip": "${searchTarget}", "apihostport": "${searchTargetPort}"], "state": ["contact": "${devicejson.deviceStatus}", "battery": "${devicejson.batteryStatus}", "tamper": "${devicejson.deviceTampered}"]])
    }
}