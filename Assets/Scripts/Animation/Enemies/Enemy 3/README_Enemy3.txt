# README_Enemy3

## Zweck

Enemy 3 ist ein animiertes Trap-/Flying-Enemy-Prefab mit vorbereitetem Animator Controller und Bridge-Script für Gameplay-Anbindung.

Enemy 3 startet versteckt im Boden, schnappt bei Aktivierung zu, fliegt hoch, greift erneut an, kann hovern und sich bei Low HP wieder einbuddeln.

## Dateien

* Prefab: `PF_Enemy3`
* Animator Controller: `AnCtrl_Enemy3`
* Scripts:

  * `Enemy3AnimatorBridge.cs`
  * `Enemy3AnimatorAutoTester.cs` optional, nur Testing
  * `Enemy3AnimationEvents.cs`

## Animator States

* `E3_HiddenArmed`
* `E3_FirstAttack`
* `E3_FlyUp`
* `E3_Walk`
* `E3_SecondAttack`
* `E3_Hover`
* `E3_FlyDown`
* `E3_DiggingAndDown`
* `E3_DeathWhileFlying`
* `E3_DeathWhileDigging`
* `E3_Deathw_Digging_V2` optional/falls genutzt

`E3_HiddenArmed` ist der Default State.

## Animator Parameter

* `WakeUp` — Trigger
  Startet die Aktivierung aus dem versteckten Zustand.

* `Speed` — Float
  Optionaler Movement-Wert. Aktuell nicht zwingend nötig, da `E3_Walk` als Base-State dient.

* `SecondAttack` — Trigger
  Startet die zweite Attacke aus dem aktiven Zustand.

* `GrabAttack` — Trigger
  Startet den Angriff aus dem Hover-State.

* `FlyDown` — Trigger
  Startet Low-HP-Flucht nach unten.

* `DigDown` — Trigger
  Startet direktes Eingraben, falls benötigt.

* `DeathFlying` — Trigger
  Startet Death-Animation in der Luft.

* `DeathDigging` — Trigger
  Startet Death-Animation beim/versteckten Zustand.

## Transition-Logik

Startsequenz:

* `E3_HiddenArmed → E3_FirstAttack`
  Condition: `WakeUp`

* `E3_FirstAttack → E3_FlyUp`
  über Exit Time, keine Condition

* `E3_FlyUp → E3_Walk`
  über Exit Time, keine Condition

Attack-/Hover-Sequenz:

* `E3_Walk → E3_SecondAttack`
  Condition: `SecondAttack`

* `E3_SecondAttack → E3_Hover`
  über Exit Time, keine Condition

* `E3_Hover → E3_SecondAttack` oder Grab-State
  Condition: `GrabAttack`

* Rückweg danach zu `E3_Hover`
  über Exit Time, keine Condition

Low-HP-Flucht:

* `E3_Walk → E3_FlyDown`
  Condition: `FlyDown`

* `E3_Hover → E3_FlyDown`
  Condition: `FlyDown`

* `E3_FlyDown → E3_DiggingAndDown`
  über Exit Time, keine Condition

* `E3_DiggingAndDown → E3_HiddenArmed`
  über Exit Time, keine Condition

Death:

* `Any State → E3_DeathWhileFlying`
  Condition: `DeathFlying`

* `Any State → E3_DeathWhileDigging`
  Condition: `DeathDigging`

Death-States haben keine Rücktransition.

## Clip Settings

Loop Time ON:

* `E3_Walk`
* `E3_Hover`

Loop Time OFF:

* `E3_FirstAttack`
* `E3_FlyUp`
* `E3_SecondAttack`
* `E3_FlyDown`
* `E3_DiggingAndDown`
* alle Death-Animationen

`E3_HiddenArmed` nutzt den versteckten Startzustand. Falls dafür `E3_FirstAttack` mit Speed `0` verwendet wird, muss der Clip selbst nicht loopen.

## Bridge-Script

`Enemy3AnimatorBridge` stellt folgende Methoden bereit:

* `WakeUp()`
* `SetSpeed(float speed)`
* `PlaySecondAttack()`
* `PlayGrabAttack()`
* `PlayFlyDown()`
* `PlayDigDown()`
* `PlayDeathFlying()`
* `PlayDeathDigging()`
* `ResetToHidden()`
* `ResetToWalkBase()`
* `ResetToHover()`
* `ResetAllTriggers()`

## AutoTester

`Enemy3AnimatorAutoTester` ist nur für Animator-Tests gedacht.

Test-Szenarien:

* `WakeUpToWalk`
* `SecondAttackToHover`
* `GrabAttackFromHover`
* `LowHpEscapeFromWalk`
* `LowHpEscapeFromHover`
* `FullCycleNoLowHp`
* `DeathFlying`
* `DeathDigging`

Zusätzlich kann der Tester den Enemy auf seinen Ursprungspunkt zurücksetzen.

Wichtige Optionen:

* `Object To Reset` = Transform von `PF_Enemy3`
* `Capture Origin On Start` = an
* `Reset Transform Before Auto Test` = an

Über das Component-Kontextmenü können genutzt werden:

* `TEST / Reset Transform To Origin`
* `TEST / Reset Animation To Hidden At Origin`
* `TEST / Reset Animation To Walk At Origin`
* `TEST / Reset Animation To Hover At Origin`

Für normales Gameplay deaktivieren:

* `Auto Run On Start`
* `Loop Test`

## Animation Events

`Enemy1AnimationEvents` enthält vorbereitete Event-Methoden für spätere Gameplay-Anbindung, z. B. Hit-Frames, Footsteps oder Death-Ende.

Aktuell dienen sie hauptsächlich als Debug-/Timing-Markierungen.


## Programmer Notes

Die Gameplay-Logik sollte Enemy 3 über `Enemy3AnimatorBridge` ansteuern.

Wichtige Nutzung:

* `WakeUp()` bei Berührung/Aktivierung aus dem Boden
* `PlaySecondAttack()` für normalen Angriff nach Aktivierung
* `PlayGrabAttack()` für Angriff aus dem Hover-State
* `PlayFlyDown()` bei Low HP
* `PlayDeathFlying()` oder `PlayDeathDigging()` abhängig vom aktuellen Zustand

Movement, AI, Damage, Grab-Logik, Minion-Aufnahme, Hitboxen, Sounds und VFX sind noch nicht implementiert.
