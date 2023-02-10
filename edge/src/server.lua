local lux = require('luxure')
local cosock = require('cosock')
local json = require('dkjson')
local caps = require('st.capabilities')
local util = require('utilities')
local log = require('log')

local hub_server = {}

function hub_server.start(driver)
  local server = lux.Server.new_with(cosock.socket.tcp(), {env='debug'})

  cosock.spawn(function()
    while true do 
      server:tick()
    end
  end)

  -- Debug endpoint to check routing
  server:get('/', function (req,res)
    res:send('HTTP/1.1 200 OK')
  end)

  -- Endpoint
  server:post('/', function (req, res)
    log.info('Received POST request')
    local body = json.decode(req:get_body() )
    local childdevice = util.get_device_by_id(driver,body.SSDPDevice.Udn) 
    if childdevice ~= nil then
        log.info('Update received for ' .. body.SSDPDevice.Udn)
        childdevice:emit_event(caps.battery.battery(tonumber(body.BatteryStatus)))
        if body.DeviceTampered == "True" then
            childdevice:emit_event(caps.tamperAlert.tamper("detected"))
        else 
            childdevice:emit_event(caps.tamperAlert.tamper("clear"))
        end 
        if body.ModelName:match("PellaDoorSensor") then
            log.info('Setting Door Sensor to '..body.DeviceStatus)
            childdevice:emit_event(caps.contactSensor.contact(string.lower(body.DeviceStatus)))
        end
        if body.ModelName:match("PellaDoorLock") then
            log.info('Setting Door Lock to '..body.DeviceStatus)
            childdevice:emit_event(caps.lock.lock(string.lower(body.DeviceStatus)))
        end
        res:send('HTTP/1.1 200 OK')
    else
        log.error("===== ERROR: Received request for unknown device: "..body.SSDPDevice.Udn)
        res:send('HTTP/1.1 400 BAD REQUEST')
    end 
  end)
  server:listen()
  log.info('Server listening on port '..server.port)
  driver.server = server
end

return hub_server