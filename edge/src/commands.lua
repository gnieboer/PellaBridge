local Driver = require('st.driver')
local caps = require('st.capabilities')
local log = require('log')
local config = require('config')
local util = require('utilities')

--Handle skipping a refresh iteration if a command was just issued
local commandIsPending = false

local commands = {}

function commands.refresh(driver, device, capability_command)

  if commandIsPending == true then
    log.info('Skipping refresh to let command settle.')
    commandIsPending = false
    return
  end
  log.info('Initiating Refresh command')
  commandIsPending = true 

  if (device.device_network_id ~= config.DEFAULT_BRIDGE_ID) then
    device = util.get_bridge_device(driver)
  end

  util.poll(driver, device)
  commandIsPending = false 
end

return commands 