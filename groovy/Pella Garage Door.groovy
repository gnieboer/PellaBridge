/**
 *  Pella Garage Door
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
 */
metadata {
	definition (name: "Pella Garage Door", namespace: "gnieboer", author: "Geof Nieboer", cstHandler: true) {
		capability "Battery"
		capability "Contact Sensor"
		capability "Tamper Alert"
        capability "Refresh"
        
		// Command to expose to the parent app to push notifications to the device
    	command "updateDevice", ["string"]
    }

	simulator {
		status "open": "contact:open"
		status "closed": "contact:closed"
        status "battery" : "battery:50"
        status "tamperon" : "tamper:on"
        status "tamperoff" : "tamper:off"
	}


    preferences {
    	section("Note") {
            paragraph "These settings are pre-populated during installation and generally should not need to be altered"
            input name: "apihostip", type: "text", title: "Bridge IP", description: "Enter local IP of API Bridge", required: true, displayDuringSetup: true
            input name: "apihostport", type: "number", title: "Bridge Port", description: "Enter port of API Bridge", required: true, displayDuringSetup: true
            input name: "deviceid", type: "number", title: "Pella Device ID", description: "Enter Pella Device ID", required: true, displayDuringSetup: true
        }
    }

	tiles {
        multiAttributeTile(name: "contact", type: "generic", width: 6, height: 4) {
			tileAttribute("device.contact", key: "PRIMARY_CONTROL") {
				attributeState "open", label: '${currentValue}', icon: "st.doors.garage.garage-open", backgroundColor: "#e86d13"
				attributeState "closed", label: '${currentValue}', icon: "st.doors.garage.garage-closed", backgroundColor: "#00A0DC"
			}
		}
		valueTile("battery", "device.battery", inactiveLabel:false, decoration:"flat", width:2, height:2) {
			state "battery", label:'${currentValue}%', unit:""
		}
        standardTile("tamper", "device.tamper", width: 2, height: 2) {
        	state "detected", label: "tamper", icon:"st.alarm.alarm.alarm", backgroundColor:"#ff0000"
        	state "clear", icon:"", backgroundColor:"#ffffff"
    	}
		standardTile("refresh", "device.refresh", inactiveLabel:false, decoration:"flat", width:2, height:2) {
			state "default", label:'', action:"refresh", icon:"st.secondary.refresh"
		}
	}
}

def installed() {
	refresh()
}

// parse events into attributes, only used by simulator
def parse(String description) {
	log.debug "Parsing '${description}'"
	if (description == "contact:open") { open()}
    if (description == "contact:closed") { closed()}
    if (description == "battery:50") { battery(50)}
    if (description == "tamper:on") { tamper(true)}
    if (description == "tamper:off") { tamper(false)}
}

def tamper(boolean tampered)
{
	if (tampered) {
    	sendEvent(name: "tamper", value: "detected")
    } else {
    	sendEvent(name: "tamper", value: "clear")
    }
}

def open() {
    sendEvent(name: "contact", value: "open")
}

def closed() {
	sendEvent(name: "contact", value: "closed")
}

def refresh() {
	def host = "${apihostip}:${apihostport}"
    log.debug host 
    def id = deviceid
    log.debug "Refreshing Device State" 
    sendHubCommand(new physicalgraph.device.HubAction("""GET /api/PellaBridge/battery/$id HTTP/1.1\r\nHOST: $host\r\n\r\n""", physicalgraph.device.Protocol.LAN, host, [callback:batteryRefreshHandler]))
	sendHubCommand(new physicalgraph.device.HubAction("""GET /api/PellaBridge/devicestatus/$id HTTP/1.1\r\nHOST: $host\r\n\r\n""", physicalgraph.device.Protocol.LAN, host, [callback: deviceStatusHandler]))
}

def updateDevice(device)
{
	log.debug("Updating device #${deviceid} after push received")
	deviceChangeStatus(device.DeviceStatusCode)
    deviceChangeBattery(device.BatteryStatus)
}

def batteryRefreshHandler(physicalgraph.device.HubResponse hubResponse)
{
	log.debug "received battery response for device ${deviceid}: ${hubResponse.body}"
    state.lastbatteryresponse = hubResponse.requestId
    deviceChangeBattery(hubResponse.body.toInteger())
}

def deviceChangeBattery(int batterylevel)
{
    if (batterylevel >= 0 && batterylevel <= 100) {
        log.debug("battery status received: ${batterylevel}")
        sendEvent(name: "battery", value: batterylevel)
    } else {
        error.debug("Invalid battery status received: ${batterylevel}")
    }
}

def deviceStatusHandler(physicalgraph.device.HubResponse hubResponse)
{
	log.debug "received status response for device ${deviceid}: ${hubResponse}"
    log.debug "response: ${hubResponse.body}"
    deviceChangeStatus(hubResponse.body)
}

def deviceChangeStatus(def status) 
{
    switch (status)
    {
    case "0":
    	tamper(false);
        closed()
        break;
    case "1":
    	tamper(false);
        open()
        break;
    case "2":
    	tamper(false);
        closed()
        break;        
    case "4":
    	tamper(true);
        closed()
        break;
    case "5":
    	tamper(true);
        open()
        break;
    case "6":
    	tamper(true);
        closed()
        break;           
    default:
      	log.debug("invalid device status received: ${status}")
    }
}