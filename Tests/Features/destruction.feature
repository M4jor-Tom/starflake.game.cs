Feature: Destruction
  What a destroyed asteroid or raider ship leaves behind.

  Scenario: A destroyed asteroid leaves a fuel pod holding its fuel
    Given an asteroid whose type has a fuel yield of 15
    When a hit takes its integrity to 0
    Then the asteroid is removed from the star system
    And a fuel pod with a fuel amount of 15 floats where the asteroid was

  Scenario: A destroyed raider ship leaves a fuel pod holding its fuel
    Given a raider ship whose type has a fuel yield of 40
    When a hit takes its hull to 0
    Then the raider ship is removed from the star system
    And a fuel pod with a fuel amount of 40 floats where the raider ship was
