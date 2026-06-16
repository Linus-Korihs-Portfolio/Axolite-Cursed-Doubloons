# README_Pinch

## Zweck

Pinch ist ein animiertes NPC-Prefab mit vorbereitetem Animator Controller und Bridge-Script für Gameplay- oder Dialog-Anbindung.

Pinch besitzt nur 3 Animationen und benötigt kein Movement-System.

## Dateien

* Prefab: `PF_Pinch`
* Animator Controller: `AnCtrl_Pinch`
* Scripts:

  * `PinchAnimatorBridge.cs`
  * `PinchAnimatorAutoTester.cs` optional, nur Testing
  * `PinchAnimationEvents.cs`

## Animator States

* `Pinch_Idle`
* `Pinch_Dialog`
* `Pinch_IdleBreak`

`Pinch_Idle` ist der Default State.

## Animator Parameter

* `Dialog` — Trigger
  Startet die Dialog-Animation.

* `IdleBreak` — Trigger
  Startet die optionale Idle-Unterbrechung.

Ein `Speed`-Parameter wird nicht benötigt, da Pinch kein Enemy mit Movement ist.

## Transition-Logik

* `Any State → Pinch_Dialog`
  Condition: `Dialog`

* `Pinch_Dialog → Pinch_Idle`
  über Exit Time, keine Condition

* `Any State → Pinch_IdleBreak`
  Condition: `IdleBreak`

* `Pinch_IdleBreak → Pinch_Idle`
  über Exit Time, keine Condition

`Pinch_Idle` läuft dauerhaft als Standardanimation.

## Clip Settings

Loop Time ON:

* `Pinch_Idle`

Loop Time OFF:

* `Pinch_Dialog`
* `Pinch_IdleBreak`

## Bridge-Script

`PinchAnimatorBridge` stellt folgende Methoden bereit:

* `PlayIdle()`
* `PlayDialog()`
* `PlayIdleBreak()`
* `ResetToIdle()`
* `ResetToStartAndIdle()`

`ResetToStartAndIdle()` ist primär für Tests gedacht und setzt Pinch auf seine Startposition zurück.

## Animation Events

`PinchAnimationEvents` enthält vorbereitete Event-Methoden für spätere Gameplay- oder Dialog-Anbindung.

Aktuell können sie genutzt werden, um Dialog- oder IdleBreak-Animationen sauber wieder zu `Pinch_Idle` zurückzuführen.

## Testing

`PinchAnimatorAutoTester` ist nur für Animator-Tests gedacht.

Im Play Mode:

* `1` = Idle
* `2` = Dialog
* `3` = IdleBreak
* `R` = Reset zu Startposition + Idle

Zusätzlich erscheint ein kleines Testfenster im Game View mit Buttons für alle Animationen.

Für normales Gameplay deaktivieren oder entfernen:

* `PinchAnimatorAutoTester`

## Programmer Notes

Die Gameplay- oder Dialog-Logik sollte Pinch über `PinchAnimatorBridge` ansteuern.

Movement, AI, Dialogsystem, Sounds, VFX und Interaktionslogik sind noch nicht implementiert.
