/**
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
	}

	simulator {
		// TODO: define status and reply messages here
	}

	tiles(scale: 2) {
		standardTile(name:"locked", type:"generic", width:6, height:4) {
			tileAttribute ("device.deviceStatus", key:"PRIMARY_CONTROL") {
				attributeState "Locked", label:'locked', icon:"st.locks.lock.locked", backgroundColor:"#00A0DC"
				attributeState "Unlocked", label:'unlocked', icon:"st.locks.lock.unlocked", backgroundColor:"#ffffff"
				attributeState "Unknown", label:'unknown', icon:"st.locks.lock.unknown", backgroundColor:"#ffffff"
			}
		}
		valueTile("battery", "device.batteryStatus", inactiveLabel:false, decoration:"flat", width:2, height:2) {
			state "battery", label:'${currentValue}%', unit:""
		}
        standardTile("tamper", "device.deviceTampered", width: 2, height: 2) {
        	state "true", icon:"st.alarm.alarm.alarm", backgroundColor:"#ff0000"
        	state "false", icon:"st.alarm.water.wet", backgroundColor:"#ffffff"
    	}
		standardTile("refresh", "device.refresh", inactiveLabel:false, decoration:"flat", width:2, height:2) {
			state "default", label:'', action:"refresh", icon:"st.secondary.refresh"
		}

		main "locked"
		details(["locked", "battery", "tamper", "refresh"])
	}
}

def installed() {
	log.debug "Installed with settings: ${settings}"

	initialize()
}

def updated() {
	log.debug "Updated with settings: ${settings}"

	unsubscribe()
	initialize()
}

def initialize() {
	unsubscribe()
    unschedule()
}

// handle commands
def refresh() {
	String host = parent.getHost()
    def id = state.id
    log.debug("Refreshing Device State")
    sendHubCommand(new physicalgraph.device.HubAction("""GET /api/PellaBridge/battery/$id HTTP/1.1\r\nHOST: $host\r\n\r\n""", physicalgraph.device.Protocol.LAN, host, [callback:batteryRefreshHandler]))
	sendHubCommand(new physicalgraph.device.HubAction("""GET /api/PellaBridge/devicestatusstring/$id HTTP/1.1\r\nHOST: $host\r\n\r\n""", physicalgraph.device.Protocol.LAN, host, [callback: deviceStatusHandler]))
    
    return dynamicPage(name: "discoverDevicesPage", title: "Discovery Started!", nextPage: "", refreshInterval: 5, install: true, uninstall: true) {
		section("Please wait while we discover your Pella Devices.") {
        	paragraph "Please wait while we discover your Pella Devices"
        }
	}
}

def batteryRefreshHandler(physicalgraph.device.HubResponse hubResponse)
{
	def battery = hubResponse.body
    if (battery >= 0 && battery <= 100) {
      log.debug("battery status received: ${battery}")
      state.battery = battery
    } else {
      log.debug("Invalid battery status received: ${battery}")
    }
}

def deviceStatusHandler(physicalgraph.device.HubResponse hubResponse)
{
    def status = hubResponse.body
    if (status)
    {
    	log.debug("device status received: ${status}")
    	state.deviceStatus = status
    } else {
    	log.debug("invalid device status received: ${status}")
    }

}
