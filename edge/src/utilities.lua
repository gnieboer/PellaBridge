local Driver = require('st.driver')
local caps = require('st.capabilities')
local neturl = require('net.url')
local json = require('dkjson')
local cosock2 = require "cosock"
local ltn12 = require('ltn12')
local http = cosock2.asyncify "socket.http"
local log = require('log')
local config = require('config')
-- XML modules
local xml2lua = require "xml2lua"
local xml_handler = require "xmlhandler.tree"

local util = {}

function util.dump(o)
   if type(o) == 'table' then
      local s = '{ '
      for k,v in pairs(o) do
         if type(k) ~= 'number' then k = '"'..k..'"' end
         s = s .. '['..k..'] = ' .. util.dump(v) .. ','
      end
      return s .. '} '
   else
      return tostring(o)
   end
end

-- Device should always be the bridge 
function util.poll(driver, device)
    local port = driver.server.port
    if port ~= nil then
        local location = "http://"..device.preferences.searchTarget..":"..device.preferences.searchTargetPort.."/api/PellaBridge/registerport/"..port
        local info = util.fetch_device_info(location)
        if info ~= nil then 
          util.update_devices(driver, info)
          for _, dev in ipairs(driver:get_devices()) do
            dev:online();
          end 
        else
          log.error("Bridge Container did not respond to poll")
          for _, dev in ipairs(driver:get_devices()) do 
            dev:offline();
          end 
        end 
    else
        log.error("Poll failed because server listener not enabled")
    end
end

function util.get_bridge_device(driver)
  local device_list = driver:get_devices() --Grab existing devices
  for _, device in ipairs(device_list) do
    if device.device_network_id == config.DEFAULT_BRIDGE_ID then
      return driver.get_device_info(driver, device.id)
    end
  end
  log.error('Pella Bridge device not found')
end

function util.get_device_by_id(driver, uuid)
  if uuid ~= nil then
    local device_list = driver:get_devices() --Grab existing devices
    for _, device in ipairs(device_list) do
      if device.device_network_id == uuid then
        return driver.get_device_info(driver, device.id)
      end
    end
    log.error('UUID '..uuid..' not found')
    return nil 
  else
    log.error('UUID not provided to get_device_by_id')
    return nil
  end 
end

-- Fetching device metadata via
-- SSDP Response Location header
function util.fetch_device_info(url)
  log.info('===== FETCHING ROOT DEVICE METADATA FROM '..url)
  local res = {}
  local _, status = http.request({
    url=url,
    sink=ltn12.sink.table(res)
  })

  if status ~= 200 then
    return nil
  end 

  -- XML Parser
  local xmlres = xml_handler:new()
  local xml_parser = xml2lua.parser(xmlres)
  xml_parser:parse(table.concat(res))

  -- Device metadata
  local meta = xmlres.root.root.device

  if not xmlres.root or not meta then
    log.error('===== FAILED TO FETCH METADATA')
    return nil
  end

  return {
    label=meta.friendlyName,
    device_network_id = meta.UDN,
    vendor_provided_label=meta.friendlyName,
    manufacturer=meta.manufacturer,
    model=meta.modelName,
    location=url:sub(0, url:find('/api')-1),
    devicelist = meta.deviceList
  }
end

function util.update_devices(driver, deviceinfo)
  local root = util.get_bridge_device(driver)
  for i, child in pairs(deviceinfo.devicelist.device) do
    log.info('===== UPDATING CHILD DEVICE '..child.friendlyName)
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
  return root
end

return util