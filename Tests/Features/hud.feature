Feature: HUD
  Which readouts and messages the player sees, and when.

  Scenario: The permanent readouts are always shown
    Given a running session
    Then the HUD shows "system", "hull", "counts", "crosshair" and "controls_help"
    And every number is rounded to the "hud.number_decimals"

  Scenario: The fuel readout says when the tank is full
    Given a pilot ship whose ship class has a fuel capacity of 100
    When the ship holds 40 fuel
    Then the HUD shows "fuel" and not "fuel_tank_full"
    When the ship holds 100 fuel
    Then the HUD shows "fuel_tank_full" and not "fuel"

  Scenario: The jump readout says whether the ship can jump
    Given a pilot ship in a star system whose jump fuel cost is 50
    When the ship holds 50 fuel
    Then the HUD shows "jump_route_ready" and neither "jump_route_fuel_short" nor "jump_route_none"
    When the ship holds 40 fuel
    Then the HUD shows "jump_route_fuel_short" and neither "jump_route_ready" nor "jump_route_none"

  Scenario: The jump readout says when there is no jump route
    Given a pilot ship in a star system that has no jump targets
    Then the HUD shows "jump_route_none" and neither "jump_route_ready" nor "jump_route_fuel_short"

  Scenario: A new session greets the player
    When a solo session starts
    Then the HUD flashes "welcome"

  Scenario: A message disappears after the message duration
    When the HUD flashes "jumped"
    Then the message is shown for the "hud.message_duration_seconds"
    And no message is shown afterwards

  Scenario: A new message replaces the current one and restarts the duration
    Given the HUD is showing "welcome"
    When the HUD flashes "jump_refused"
    Then only "jump_refused" is shown
    And it is shown for the "hud.message_duration_seconds" from that moment

  Scenario: Key placeholders name the inputs bound to an action
    Given "jump" is bound to the key labelled J on the player's keyboard
    And "pitch_up" is bound to the up arrow key
    And "fire" is bound to the Space key and to the left mouse button
    Then the key placeholder of "jump" reads J
    And the key placeholder of "pitch_up" reads ↑
    And the key placeholder of "fire" reads Space or click
