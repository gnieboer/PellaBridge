/**
 *  Pella Shade
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
	definition (name: "Pella Shade", namespace: "gnieboer", author: "Geof Nieboer", cstHandler: true) {
		capability "Battery"
		capability "Window Shade"
        capability "Refresh"
        capability "Actuator"
        capability "Sensor"
        
        attribute "shadeLevel", "number"
        
		// Command to expose to the parent app to push notifications to the device
    	command "updateDevice", ["string"]
        command "stopmove"
        command "middle"
        command "full_open"
        command "full_close"
        command "set_position", ["number"]
    }

	simulator {
		status "open": "shade:open"
		status "close": "shade:close"
        status "middle": "middle"
        status "stop": "stopmove"
        status "user": "shade:favorite"
        status "battery": "battery:50"
        status "forceup":"forceup"
        status "forcedown" : "forcedown"
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
        multiAttributeTile(name: "contact", type: "generic", width: 4, height: 4) {
			tileAttribute("device.windowShade", key: "PRIMARY_CONTROL") {
				attributeState "open", label: '${currentValue}',  backgroundColor: "#e86d13"
				attributeState "closed", label: '${currentValue}',  backgroundColor: "#00A0DC"
			}
        tileAttribute("device.level", key: "SLIDER_CONTROL") {
        		attributeState "shadeLevel", action:"set_position", defaultState: true
    		}
		}
        standardTile("open", "shade.open", inactiveLabel:false, decoration:"flat", width:2, height:2) {
			state "default", label:'', action:"open", icon:"st.thermostat.thermostat-up"
		}
        
        standardTile("stop", "", inactiveLabel:false, decoration:"flat", width:2, height:2) {
			state "default", label:' ', action:"pause", icon:"st.sonos.stop-btn"
		}
        
        standardTile("close", "shade.close", inactiveLabel:false, decoration:"flat", width:2, height:2) {
			state "default", label:'', action:"close", icon:"st.thermostat.thermostat-down"
		}
        
		valueTile("battery", "device.battery", inactiveLabel:false, decoration:"flat", width:2, height:2) {
			state "battery", label:'${currentValue}%', unit:""
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
	if (description == "shade:open") { open()}
    if (description == "shade:close") { close()}
    if (description == "battery:50") { battery(50)}
    if (description == "stopmove") { pause()}
    if (description == "shade:favorite") { middle()}
    if (description == "middle") {set_position(50)}
    if (description == "forceup") {
    	sendEvent(name: "shadeLevel", value: 0)
        sendEvent(name: "windowShade", value: "open")
    }
    if (description == "forcedown") {
    	sendEvent(name: "shadeLevel", value: 100)
        sendEvent(name: "windowShade", value: "closed")
    }
}

// This is the user-programmed open level
def open() {
    sendCommand("102")
}

// This is the user-programmed close level
def close() {
	sendCommand("101")
}

def presetPosition(int level)
{
	sendCommand(level.toString())
}

def set_position(int level)
{
	sendCommand(level.toString())
}

def set_position()
{
	sendCommand(state.shadeLevel.toString())
}

// This is the user-programmed favorite level
def middle() {
	sendCommand("103")
}

def pause() {
	sendCommand("106")
}

def full_open() {
	sendCommand("0")
}

def full_close() {
	sendCommand("100")
}

def sendCommand(String cmd)
{
	def host = "${apihostip}:${apihostport}"
    def id = deviceid
    log.debug "Sending command to shade: ${cmd}" 
    sendHubCommand(new physicalgraph.device.HubAction("""GET /api/PellaBridge/setshade/$id/$cmd HTTP/1.1\r\nHOST: $host\r\n\r\n""", physicalgraph.device.Protocol.LAN, host, [callback:commandHandler]))
}

def refresh() {
	def host = "${apihostip}:${apihostport}"
    def id = deviceid
    log.debug "Refreshing Device State" 
    sendHubCommand(new physicalgraph.device.HubAction("""GET /api/PellaBridge/battery/$id HTTP/1.1\r\nHOST: $host\r\n\r\n""", physicalgraph.device.Protocol.LAN, host, [callback:batteryRefreshHandler]))
	sendHubCommand(new physicalgraph.device.HubAction("""GET /api/PellaBridge/devicestatus/$id HTTP/1.1\r\nHOST: $host\r\n\r\n""", physicalgraph.device.Protocol.LAN, host, [callback: deviceStatusHandler]))
}

def updateDevice(device)
{
	log.debug("Updating device #${deviceid} after push received")
	deviceChangeStatus(device.DeviceStatusCode.toInteger())
    deviceChangeBattery(device.BatteryStatus)
}

def commandHandler(physicalgraph.device.HubResponse hubResponse)
{
	log.debug("Command response received: ${hubResponse.body}")
    
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
    deviceChangeStatus(hubResponse.body.toInteger())
}

def deviceChangeStatus(int level)
{
    if (level >=0 && level <= 100) {
 		switch (level) 
        {
        case 0:
        	state.shadeStatus = "closed"            
            break;
        case 100:
        	state.shadeStatus = "open"
            break;
        default:
        	state.shadeState = "partially open"
        }
        state.shadeLevel = level
        sendEvent(name: "shadeLevel", value:state.shadeLevel)
    } else {
    	state.shadeStatus = "unknown"
    	error.debug("Invalid shade status received: ${shadelevel}")
    }
    sendEvent(name: "windowShade", value:state.shadeStatus)
}