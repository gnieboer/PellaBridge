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
    page(name: "searchTargetSelection", title: "Pella Bridge", nextPage: "discoverDevicesPage") {
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

	initialize()
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
	initialize()
}

def initialize() {
	unsubscribe()
    unschedule()
    
    discoverDevices()
}

def getDevices() {
	if (!state.devices) {
		state.devices = [:]
	}
	state.devices
}

def discoverDevices() {

	String host = "${searchTarget}:${searchTargetPort}"
    log.debug("Requesting Bridge Status")
    sendHubCommand(new physicalgraph.device.HubAction("""GET /api/PellaBridge/status HTTP/1.1\r\nHOST: $host\r\n\r\n""", physicalgraph.device.Protocol.LAN, host, [callback:bridgeStatusHandler]))
    log.debug("Requesting device list from bridge")
	sendHubCommand(new physicalgraph.device.HubAction("""GET /api/PellaBridge/enumerate HTTP/1.1\r\nHOST: $host\r\n\r\n""", physicalgraph.device.Protocol.LAN, host, [callback: deviceDiscoveryHandler]))
    
    return dynamicPage(name: "discoverDevicesPage", title: "Discovery Started!", nextPage: "", install: true, uninstall: true) {
		section("Please wait while we discover your Pella Devices.") {
        	paragraph "Please wait while we discover your Pella Devices"
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
    
    def devices = getChildDevices()
    
   
    json.each { 
    	def childid = it.id
    	log.debug "finding: ${state.mac + "_" + childid}"
    	def matchingDevice = childDevices.find({ it.deviceNetworkId == "${state.mac + "_" + childid}" })
        if (matchingDevice) {
        	if (matchingDevice.deviceTypeCode == it.deviceTypeCode) {
             	// Save type of devices as current stored... update values since we got them
                log.debug("updating device ID ${childid}")
                matchingDevice.state.deviceStatusCode = it.deviceStatusCode
            	matchingDevice.state.batteryStatus = it.batteryStatus
            } else 
            {
            	// Different device than we currently have stored... remove and replace
                log.debug("Remove/Replacing device ID ${childid}")
            	deleteChildDevice(matchingDevice.deviceNetworkId)
                addDevice(it)
            }
        } else {
        	// No match, add
            log.debug("Adding device ID ${it.id}")
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
    	addChildDevice("gnieboer", devicejson.deviceType, state.mac + "_" + devicejson.id, hub.getId(), ["deviceid": "${devicejson.id}", "lock": "${devicejson.deviceStatus}", "battery": "${devicejson.batteryStatus}", "tamper": "${devicejson.deviceTampered}", "apihostip": "${searchTarget}", "apihostport": "${searchTargetPort}"])
    }
    if (devicejson.deviceTypeCode == 01) {
    	log.debug "Adding new door sensor: ${devicejson}"
    	addChildDevice("gnieboer", devicejson.deviceType, state.mac + "_" + devicejson.id, hub.getId(), ["deviceid": "${devicejson.id}", "contact": "${devicejson.deviceStatus}", "battery": "${devicejson.batteryStatus}", "tamper": "${devicejson.deviceTampered}", "apihostip": "${searchTarget}", "apihostport": "${searchTargetPort}"])
    }
	
}