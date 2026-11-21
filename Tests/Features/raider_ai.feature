Feature: Raider AI
  How a raider ship behaves toward the pilot ship. Raider ships fly by the
  rules of the Flight feature.

  Scenario: At or beyond its detection range a raider is idle
    Given a raider ship whose type has a detection range of 2000 metres
    When the pilot ship is farther than 2000 metres from it
    Then the raider ship does not turn
    And the raider ship does not thrust
    And the raider ship does not fire
    When the pilot ship is exactly 2000 metres from it
    Then the raider ship still does not turn, thrust or fire

  Scenario: Closer than its detection range a raider closes in to the keep distance
    Given a raider ship whose type has a detection range of 2000 metres and a keep distance of 250 metres
    When the pilot ship is closer than 2000 metres to it and farther than 250 metres
    Then the raider ship turns its nose toward the pilot ship
    And the raider ship thrusts at full throttle

  Scenario: At or within the keep distance a raider stops thrusting
    Given a raider ship whose type has a detection range of 2000 metres and a keep distance of 250 metres
    When the pilot ship is closer to it than 250 metres
    Then the raider ship turns its nose toward the pilot ship
    And the raider ship does not thrust
    When the pilot ship is exactly 250 metres from it
    Then the raider ship still does not thrust

  Scenario: A raider steers harder the farther the pilot ship is off its nose
    Given a raider ship that has detected the pilot ship ahead of it
    Then its pitch and yaw inputs are the sideways offsets of the pilot ship's direction multiplied by the "raider_ai.steering_gain"
    And each input is capped at a full turn input
    And the raider ship never rolls

  Scenario: With the pilot ship behind it, a raider yaws at the full turn rate toward the nearer side
    Given a raider ship that has detected the pilot ship behind it
    When the pilot ship is on its right
    Then the raider ship yaws right at the turn rate of its ship class, without pitching
    When the pilot ship is straight behind it
    Then the raider ship yaws right at the turn rate of its ship class, without pitching
    When the pilot ship is on its left
    Then the raider ship yaws left at the turn rate of its ship class, without pitching

  Scenario: A raider fires only in range and with the pilot ship inside its aim cone
    Given a raider ship that has detected the pilot ship
    And the longest max range among the weapons of its ship class is 1500 metres
    When the pilot ship is closer than 1500 metres and the angle between the raider's nose and the pilot ship's direction is less than the "raider_ai.aim_cone"
    Then the raider ship fires every weapon of its ship class, each at its own fire rate
    When the pilot ship is 1500 metres away or farther
    Then the raider ship does not fire
    When the angle between the raider's nose and the pilot ship's direction is the "raider_ai.aim_cone" or more
    Then the raider ship does not fire
