Feature: Fuel scoop
  How the pilot ship collects the fuel left by destroyed asteroids and raiders.
  The scoop radius, tractor range and tractor speed are those of its ship class.

  Background:
    Given a pilot ship whose ship class has a scoop radius of 30 metres
    And a tractor range of 250 metres and a tractor speed of 220 metres per second
    And a fuel capacity of 100

  Scenario: A fuel pod closer than the tractor range moves toward the pilot ship
    Given the ship holds 10 fuel
    When a fuel pod is 100 metres from the ship
    Then the fuel pod moves straight toward the ship at 220 metres per second
    When the fuel pod is exactly 30 metres from the ship
    Then the fuel pod still moves toward the ship and is not collected yet

  Scenario: A fuel pod at or beyond the tractor range stays where it is
    Given the ship holds 10 fuel
    When a fuel pod is 300 metres from the ship
    Then the fuel pod does not move
    When the fuel pod is exactly 250 metres from the ship
    Then the fuel pod still does not move

  Scenario: A fuel pod closer than the scoop radius is collected
    Given the ship holds 10 fuel
    When a fuel pod with a fuel amount of 25 is 29 metres from the ship
    Then the ship holds 35 fuel
    And the fuel pod is removed from the star system

  Scenario: Collected fuel beyond the fuel capacity is lost
    Given the ship holds 90 fuel
    When a fuel pod with a fuel amount of 25 is 29 metres from the ship
    Then the ship holds 100 fuel
    And the fuel pod is removed from the star system

  Scenario: With a full tank, fuel pods are neither attracted nor collected
    Given the ship holds 100 fuel
    When a fuel pod is 29 metres from the ship
    Then the fuel pod does not move
    And the fuel pod stays in the star system
    And the ship holds 100 fuel
