Feature: Combat
  Firing, projectiles and damage. The Destruction feature covers what a hit to zero leaves.

  Scenario: Holding fire fires every weapon of the ship class, each at its own fire rate
    Given a pilot ship whose ship class has a weapon with a fire rate of 2 shots per second
    And another weapon with a fire rate of 5 shots per second
    When the player starts holding "fire"
    Then each weapon fires one projectile at once
    And the first weapon fires again every 0.5 seconds while "fire" is held
    And the second weapon fires again every 0.2 seconds while "fire" is held

  Scenario: A projectile starts ahead of the nose and inherits the shooter's velocity
    Given a ship moving at 50 metres per second along its nose
    And a weapon with a projectile speed of 400 metres per second
    When the ship fires the weapon
    Then the projectile starts on the line of the ship's nose, ahead of the ship by the "flight.ship_hit_radius" plus the "flight.muzzle_clearance"
    And the projectile flies straight at 450 metres per second in the direction of the nose

  Scenario: A projectile disappears beyond the max range of its weapon
    Given a projectile of a weapon with a max range of 1500 metres
    When the projectile is farther than 1500 metres from where it started
    Then the projectile disappears

  Scenario: A pilot projectile damages the asteroid it meets
    Given an asteroid with 30 integrity
    And a projectile fired by the pilot ship from a weapon with a damage of 10
    When the path of the projectile passes within the radius of the asteroid's type
    Then the asteroid has 20 integrity
    And the projectile disappears

  Scenario: A pilot projectile damages the raider ship it meets
    Given a raider ship with 30 hull
    And a projectile fired by the pilot ship from a weapon with a damage of 10
    When the path of the projectile passes within the "flight.ship_hit_radius" of the raider ship
    Then the raider ship has 20 hull
    And the projectile disappears

  Scenario: Damage never takes integrity or hull below zero
    Given an asteroid with 5 integrity
    When it is hit by a projectile from a weapon with a damage of 10
    Then the asteroid has 0 integrity

  Scenario: Raider projectiles hit only the pilot ship
    Given a projectile fired by a raider ship from a weapon with a damage of 10
    When the path of the projectile passes through an asteroid or another raider ship
    Then the asteroid or raider ship is not damaged and the projectile flies on
    When the path of the projectile passes within the "flight.ship_hit_radius" of the pilot ship
    Then the pilot ship loses 10 hull
    And the projectile disappears

  Scenario: Releasing fire does not let a weapon beat its fire rate
    Given a pilot ship whose ship class has a weapon with a fire rate of 2 shots per second
    And the weapon has just fired
    When the player releases "fire" and holds it again 0.2 seconds later
    Then the weapon fires its next projectile 0.5 seconds after the previous one

  Scenario: Projectiles in flight disappear when the pilot ship jumps
    Given projectiles are in flight in a star system
    When the pilot ship jumps to another star system
    Then those projectiles are gone, also when the pilot ship comes back
