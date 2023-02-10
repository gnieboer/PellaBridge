local socket = require('socket')
local cosock = require "cosock"
local http = cosock.asyncify "socket.http"
local ltn12 = require('ltn12')
local log = require('log')
local config = require('config')
local caps = require('st.capabilities')
local util = require('utilities')
-- XML modules
local xml2lua = require "xml2lua"
local xml_handler = require "xmlhandler.tree"

-----------------------

local function create_default_device(driver)
  log.info('===== CREATING DEFAULT ROOT DEVICE...')
  log.info('===== SET IP ADDRESS MANUALLY')
  -- device metadata table
  local metadata = {
    type = config.DEVICE_TYPE,
    device_network_id = config.DEFAULT_BRIDGE_ID,
    label = config.DEFAULT_BRIDGE_LABEL,
    profile = config.DEVICE_PROFILE,
    manufacturer = config.DEFAULT_BRIDGE_MANUFACTURER,
    model = config.DEFAULT_BRIDGE_MODEL,
    vendor_provided_label = config.DEFAULT_BRIDGE_LABEL
  }
  return driver:try_create_device(metadata) 
end

local function create_devices(driver, device)
  local root = util.get_bridge_device(driver)
  if root == nil then 
    log.info('===== CREATING ROOT DEVICE...')
    log.info('===== DEVICE DESTINATION ADDRESS: '..device.location)
    -- device metadata table
    local metadata = {
      type = config.DEVICE_TYPE,
      device_network_id = device.device_network_id,
      label = device.label,
      profile = device.model,
      manufacturer = device.manufacturer,
      model = device.model,
      vendor_provided_label = device.vendor_provided_label
    }
    root = driver:try_create_device(metadata) 
    if root ~= nil then 
      root.preferences.searchTarget = device.location:match("(%d+%.%d+%.%d+%.%d+)")
      root.preferences.searchTargetPort = (device.location.."/"):match("://.-:(%d-)/")
    end 
  end 
  if root ~= nil then 
    -- Add additional devices under the bridge
    for i, child in pairs(device.devicelist.device) do
      log.info('===== CREATING CHILD DEVICE '..child.friendlyName)
      metadata = {
          type = config.DEVICE_TYPE,
          device_network_id = child.UDN,
          label = child.friendlyName,
          profile = child.modelName,
          manufacturer = child.manufacturer,
          model = child.modelName,
          vendor_provided_label = child.friendlyName
          --parent_device_id = device.device_network_id
      }
      driver:try_create_device(metadata)
      childdevice = util.get_device_by_id(driver, child.UDN) 
      if childdevice ~= nil then
        childdevice:emit_event(caps.battery.battery(tonumber(child.Battery)))
        if child.Tampered == "True" then
          childdevice:emit_event(caps.tamperAlert.tamper("detected"))
        else 
          childdevice:emit_event(caps.tamperAlert.tamper("clear"))
        end 
        if child.modelName:match("PellaDoorSensor") then
          log.info('Setting Door Sensor to '..child.Status)
          childdevice:emit_event(caps.contactSensor.contact(string.lower(child.Status)))
        end
        if child.modelName:match("PellaDoorLock") then
          log.info('Setting Door Lock to '..child.Status)
          childdevice:emit_event(caps.lock.lock(string.lower(child.Status)))
        end
      end 
    end 
  end
  return root
end



-- SSDP Response parser
-- Raw response should be something like:
-- NOTIFY * HTTP/1.1
-- HOST: 239.255.255.250:1900
-- DATE: Thu, 09 Feb 2023 16:13:50 GMT
-- NT: urn:SmartthingsCommunity:device:PellaBridge:1
-- NTS: ssdp:alive
-- SERVER: Linux 5.10.60-qnap #1 SMP Thu Dec 15 07:26:55 CST 2022/.NET Core 3.1.32 UPnP/1.0 RSSDP/1.0
-- USN: uuid:smartthings-gcndevelopment-pellabridge::urn:SmartthingsCommunity:device:PellaBridge:1 
-- LOCATION: http://192.168.0.112:32761/api/PellaBridge/description
-- CACHE-CONTROL: public, max-age=1800
local function parse_ssdp(data)
  local res = {}
  res.status = data:sub(0, data:find('\r\n'))
  for k, v in data:gmatch('([%w-]+): ([%a+-: /=]+)') do
    res[k:lower()] = v
  end
  return res
end

-- This function enables a UDP
-- Socket and broadcast a single
-- M-SEARCH request, i.e., it
-- must be looped appart.
local function find_device()
  -- UDP socket initialization
  local upnp = socket.udp()
  upnp:setsockname('*', 0)
  upnp:setoption('broadcast', true)
  -- upnp:setoption('ip-multicast-ttl',3)
  upnp:settimeout(config.MC_TIMEOUT)

  -- broadcasting request
  log.info('===== SCANNING NETWORK...')
  upnp:sendto(config.MSEARCH, config.MC_ADDRESS, config.MC_PORT)

  -- Socket will wait n seconds
  -- based on the s:setoption(n)
  -- to receive a response back.
  local headers = nil
  local timeouttime = socket.gettime() + config.MC_TIMEOUT
  while true do
    local time_remaining = math.max(0, timeouttime - socket.gettime())
    upnp:settimeout(time_remaining)
    local val, rip, _ = upnp:receivefrom()
    if val ~= nil then
      log.info("*** DATA RECEIVED")
      log.info(val)
      headers = parse_ssdp(val)
      log.info(headers["NT"])
      local ip, port, id = headers["location"]:match(
                             "http://([^,/]+):([^/]+)/([%g-]+)")

      -- TODO how do I know the device that responded is actually a bose device
      -- potentially will need to make a request to the endpoint
      -- fetch_device_metadata()
      if rip ~= ip then
        log.warn(string.format(
                   "[%s]received discovery response with reported (%s) & source IP (%s) mismatch, ignoring",
                   deviceid, rip, ip))
        log.info(rip, "!=", ip)
      elseif ip and id then
        if deviceid then
          -- check if the speaker we just found was the one we were looking for
          if deviceid == id then
            --callback({id = id, ip = ip, raw = val})
            break
          end
        else
          --callback({id = id, ip = ip, raw = val})
        end
      end
    elseif rip == "timeout" then
      if deviceid then
        log.warn_with({hub_logs=true}, string.format("Timed out searching for device %s", deviceid))
      end
      break
    else
      error(string.format("[%s]error receving discovery replies: %s", deviceid, rip))
    end
  end

  -- close udp socket
  upnp:close()

  return headers
end

-- Discovery service which will
-- invoke the above private functions.
--    - find_device
--    - parse_ssdp
--    - fetch_device_info
--    - create_device
--
-- This resource is linked to
-- driver.discovery and it is
-- automatically called when
-- user scan devices from the
-- SmartThings App.
local disco = {}
function disco.start(driver, opts, cons)
  -- See if a bridge is already found
  local root = util.get_bridge_device(driver)
  if root == nil then
    -- If not try to find one via SSDP 
    local device_res = find_device()

    if device_res ~= nil then
      -- Great, found it, now add bridge plus children 
      log.info('===== DEVICE FOUND IN NETWORK...')
      log.info('===== DEVICE DESCRIPTION AT: '..device_res.location)
      local device = util.fetch_device_info(device_res.location)
      return create_devices(driver, device)
    else
      -- Nope, just add it anyways
      -- User will need to add IP and port then re-discover 
      log.info('===== DEVICE NOT FOUND IN NETWORK...')
      log.info('===== CREATING DEFAULT DEVICE...')
      return create_default_device(driver)
    end
  else
    -- Already have one, so ping it for more device details
    local location = "http://"..root.preferences.searchTarget..":"..root.preferences.searchTargetPort.."/api/PellaBridge/description"
    local device = util.fetch_device_info(location)
    return create_devices(driver, device)
  end 
end

return disco