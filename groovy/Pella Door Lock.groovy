﻿/**
 *  Pella Door Lock
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
	definition (name: "Pella Door Lock", namespace: "gnieboer", author: "Geof Nieboer", cstHandler: true) {
		capability "Lock"
        capability "Battery"
        capability "Tamper Alert"
        capability "Refresh"

		// Command to expose to the parent app to push notifications to the device
    	command "updateDevice", ["string"]
    }

	simulator {
		status "lock": "contact:open"
		status "unlock": "contact:closed"
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
        // This tile is from the normal device.lock code, so the display will look the sameas any other lock.  However, this lock can't be controlled
        // remotely, and this tile also supports push button locking/unlocking, so we need customized "nextState" so that firing the toggle doesn't 
        // change anything
        multiAttributeTile(name:"status", type: "generic", width: 3, height: 2){
			tileAttribute ("device.lock", key: "PRIMARY_CONTROL") {
				attributeState "locked", label:'locked', action:"lock.unlock", icon:"st.locks.lock.locked", backgroundColor:"#00A0DC", nextState:"locked"
				attributeState "unlocked", label:'unlocked', action:"lock.lock", icon:"st.locks.lock.unlocked", backgroundColor:"#ffffff", nextState:"unlocked"
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
        
        main "status"
        details(["status", "battery", "tamper", "refresh"])
	}
}

def installed() {
	refresh()
}

// parse events into attributes, only used by simulator
def parse(String description) {
	log.debug "Parsing '${description}'"
	if (description == "contact:open") { return setlocked()}
    if (description == "contact:closed") { return setunlocked()}
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

def setlocked() {
    sendEvent(name: "lock", value: "locked")
}

def setunlocked() {
	sendEvent(name: "lock", value: "unlocked")
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
        setlocked()
        break;
    case "1":
    	tamper(false);
        setunlocked()
        break;
    case "2":
    	tamper(false);
        setunlocked()
        break;        
    case "4":
    	tamper(true);
        setlocked()
        break;
    case "5":
    	tamper(true);
        setunlocked()
        break;
    case "6":
    	tamper(true);
        setlocked()
        break;           
    default:
      	log.debug("invalid device status received: ${status}")
    }
}
