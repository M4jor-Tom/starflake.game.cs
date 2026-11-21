Feature: System bounds
  What happens to a ship at the edge of a star system.

  Scenario: The pilot ship slides along the bounds of the star system
    Given a pilot ship in a star system with a bounds radius of 10000 metres
    When the ship would move farther than 10000 metres from the star
    Then the ship is held at 10000 metres from the star
    And the ship keeps only the part of its velocity that runs along the bounds

  Scenario: Raider ships are not held by the bounds of the star system
    Given a raider ship in a star system with a bounds radius of 10000 metres
    When the raider ship moves farther than 10000 metres from the star
    Then the raider ship keeps its position and its velocity
