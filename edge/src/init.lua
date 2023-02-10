local Driver = require('st.driver')
local caps = require('st.capabilities')

-- local imports
local discovery = require('discovery')
local lifecycle = require('lifecycle')
local commands = require('commands')
local server = require('server')

--------------------
-- Driver definition
local driver =
  Driver(
    'Pella-Insynctive-GCN',
    {
      discovery = discovery.start,
      lifecycle_handlers = {
        init = lifecycle.init,
        infoChanged = lifecycle.infoChanged
      },
      supported_capabilities = {
        caps.contactSensor,
        caps.battery,
        caps.lock,
        caps.refresh
      },
      capability_handlers = {
        -- Refresh command handler
        [caps.refresh.ID] = {
          [caps.refresh.commands.refresh.NAME] = commands.refresh
        }
      }
    }
  )


-----------------------------
-- Initialize Hub server
-- that will open port to
-- allow bidirectional comms.
server.start(driver)

--------------------
-- Initialize Driver
driver:run()