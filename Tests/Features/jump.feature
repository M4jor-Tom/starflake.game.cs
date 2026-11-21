Feature: Jump
  Choosing a jump target and jumping to a neighbouring star system.

  Scenario: Next target cycles through the jump targets and wraps
    Given a pilot ship in a star system whose jump targets are, in order, Alpha and Beta
    And Alpha is the selected jump target
    When the player presses "next_target"
    Then Beta is the selected jump target
    When the player presses "next_target"
    Then Alpha is the selected jump target

  Scenario: The selection resets on arrival
    Given a pilot ship whose selected jump target is not the first jump target of its star system
    When the ship arrives in another star system
    Then the first jump target of that star system is the selected jump target

  Scenario: A jump with enough fuel spends the cost and arrives at rest
    Given a pilot ship holding 60 fuel in a star system whose jump fuel cost is 50
    And a neighbouring star system is the selected jump target
    When the player presses "jump"
    Then the ship holds 10 fuel
    And the ship is in the target star system
    And the ship is at rest at the target's arrival point, facing the star
    And the HUD flashes "jumped"

  Scenario: Holding exactly the jump fuel cost is enough
    Given a pilot ship holding 50 fuel in a star system whose jump fuel cost is 50
    When the player presses "jump"
    Then the ship holds 0 fuel
    And the ship is in the target star system

  Scenario: The jump fuel cost is that of the star system the ship leaves
    Given a pilot ship holding 60 fuel in a star system whose jump fuel cost is 50
    And the selected jump target has a jump fuel cost of 80
    When the player presses "jump"
    Then the ship holds 10 fuel
    And the ship is in the target star system

  Scenario: Short of fuel, the ship stays
    Given a pilot ship holding 40 fuel in a star system whose jump fuel cost is 50
    When the player presses "jump"
    Then the ship holds 40 fuel
    And the ship is in the same star system, where it was
    And the HUD flashes "jump_refused"

  Scenario: Without jump targets, neither action does anything
    Given a pilot ship in a star system that has no jump targets
    When the player presses "next_target"
    Then nothing happens
    When the player presses "jump"
    Then nothing happens and the HUD flashes no message

  Scenario: The selection survives a respawn
    Given a pilot ship whose selected jump target is the second jump target of its star system
    When the ship is destroyed and comes back at the arrival point
    Then the second jump target is still the selected jump target
