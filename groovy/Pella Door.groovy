/**
 *  Pella Door
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
	definition (name: "Pella Door", namespace: "gnieboer", author: "Geof Nieboer", cstHandler: true) {
		capability "Battery"
		capability "Contact Sensor"
		capability "Tamper Alert"
        capability "Refresh"
	}

	simulator {
		status "open": "contact:open"
		status "closed": "contact:closed"
        status "battery" : "battery:50"
        status "tamperon" : "tamper:on"
        status "tamperoff" : "tamper:off"
	}

    preferences {
        input name: "apihostip", type: "text", title: "Bridge IP", description: "Enter local IP of API Bridge", required: true, displayDuringSetup: true
        input name: "apihostport", type: "number", title: "Bridge Port", description: "Enter port of API Bridge", required: true, displayDuringSetup: true
        input name: "deviceid", type: "number", title: "Pella Device ID", description: "Enter Pella Device ID", required: true, displayDuringSetup: true
    }

	tiles {
		standardTile("status", "device.contact", width: 3, height: 3) {
            state "open", label: '${currentValue}', icon: "st.contact.contact.open", backgroundColor: "#ffffff"
            state "closed", label: '${currentValue}', icon: "st.contact.contact.closed", backgroundColor: "#00A0DC"
		}
		valueTile("battery", "device.battery", inactiveLabel:false, decoration:"flat", width:1, height:1) {
			state "battery", label:'${currentValue}%', unit:""
		}
        standardTile("tamper", "device.tamper", width: 1, height: 1) {
        	state "detected", label: "tamper", icon:"st.alarm.alarm.alarm", backgroundColor:"#ff0000"
        	state "clear", icon:"", backgroundColor:"#ffffff"
    	}
		standardTile("refresh", "device.refresh", inactiveLabel:false, decoration:"flat", width:1, height:1) {
			state "default", label:'', action:"refresh", icon:"st.secondary.refresh"
		}
	}
}

// parse events into attributes
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

def batteryRefreshHandler(physicalgraph.device.HubResponse hubResponse)
{
	log.debug "received battery response for device ${deviceid}: ${hubResponse.body}"
    state.lastbatteryresponse = hubResponse.requestId
    def batterylevel = hubResponse.body.toInteger()
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
    def status = hubResponse.body
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