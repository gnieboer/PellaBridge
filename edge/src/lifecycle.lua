local Driver = require('st.driver')
local caps = require('st.capabilities')
local config = require('config')
local util = require('utilities')
local log = require('log')

local lifecycle_handler = {}

function lifecycle_handler.init(driver, device)

    log.debug(device.label .. ": " .. device.device_network_id .. " > INITIALIZING")
  
    --Set up poll interval
    if (device.device_network_id == config.DEFAULT_BRIDGE_ID) then
      device.thread:call_on_schedule(
        device.preferences.pollingInterval,
        function ()
          return util.poll(driver, device)
        end,
        'refreshTimer')
      -- Make an initial call to set the callback port for the bridge to send updates to the hub
      util.poll(driver, device)
    end
end

--Called when settings are changed
function  lifecycle_handler.infoChanged (driver, device, event, args)

  log.debug ('Info changed handler invoked')
  if (device.device_network_id == config.DEFAULT_BRIDGE_ID) then

    --Cancel existing timer(s)
    for timer in pairs(device.thread.timers) do
      device.thread:cancel_timer(timer)
    end

    --Set up poll interval
    device.thread:call_on_schedule(
      device.preferences.pollingInterval,
      function ()
        return util.poll(driver, device)
      end,
      'refreshTimer')
    util.poll(driver, device)
  end
end

return lifecycle_handler