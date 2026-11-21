Feature: Pause
  Freezing a session and leaving the game.

  Scenario: Pause opens the pause menu and freezes the star system
    Given a running session with ships and projectiles in motion
    When the player presses "pause"
    Then the menu "pause_menu" opens with its buttons in the order of the spec
    And no ship, projectile or fuel pod moves
    And no weapon fires and nothing takes damage
    And a HUD message on screen stops counting down its "hud.message_duration_seconds"

  Scenario: Resume closes the pause menu and the session carries on
    Given the menu "pause_menu" is open
    When the player chooses "resume"
    Then the menu "pause_menu" closes
    And every ship, projectile and fuel pod carries on from where it was, at the velocity it had

  Scenario: Pause pressed again resumes
    Given the menu "pause_menu" is open
    When the player presses "pause"
    Then the menu "pause_menu" closes
    And the session carries on as it does when the player chooses "resume"

  Scenario: Other actions do nothing while paused
    Given the menu "pause_menu" is open
    And the pilot ship holds enough fuel to jump
    When the player presses "jump"
    Then the ship is in the same star system
    When the player holds "fire"
    Then no weapon fires

  Scenario: Quit closes the game
    Given the menu "pause_menu" is open
    When the player chooses "quit"
    Then the game closes
