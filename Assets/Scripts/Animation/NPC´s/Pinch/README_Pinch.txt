# README_Pinch

## Zweck

Pinch ist ein animiertes NPC-Prefab mit vorbereitetem Animator Controller und Bridge-Script für Dialog-, Idle- und IdleBreak-Animationen.

Pinch besitzt zusätzlich eine Münze als optionales Prop, die nur während `Pinch_IdleBreak` sichtbar sein soll.

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
  Startet die IdleBreak-Animation mit Münze.

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

## Clip Settings

Loop Time ON:

* `Pinch_Idle`

Loop Time OFF:

* `Pinch_Dialog`
* `Pinch_IdleBreak`

## Münze / Prop Setup

Die Münze muss als echtes Objekt im `PF_Pinch`-Prefab vorhanden sein, nicht nur im importierten Animations-Preview.

Wichtig im `PinchAnimatorBridge`:

* `Coin Root` = Münz-GameObject aus der Prefab-Hierarchy eintragen
* `Hide Coin On Start` = ON
* `Force Coin Hidden Outside IdleBreak` = ON
* `Disable Coin Game Object When Hidden` = ON

Die Münze wird dadurch nur bei `Pinch_IdleBreak` angezeigt und bei Idle, Dialog und Reset automatisch versteckt.

Falls `Force Hide Coin` im Tester die Münze nicht versteckt, ist wahrscheinlich das falsche Münz-Objekt im `Coin Root` eingetragen.

## Bridge-Script

`PinchAnimatorBridge` stellt folgende Methoden bereit:

* `PlayIdle()`
* `PlayDialog()`
* `PlayIdleBreak()`
* `ResetToIdle()`
* `ResetToStartAndIdle()`
* `SetCoinVisible(bool visible)`

`ResetToStartAndIdle()` ist primär für Tests gedacht.

## Animation Events

`PinchAnimationEvents` enthält vorbereitete Event-Methoden für spätere Gameplay-, Sound-, VFX- oder Prop-Anbindung.

Aktuell sind unter anderem Methoden zum Anzeigen und Verstecken der Münze vorbereitet:

* `AnimationEvent_ShowCoin()`
* `AnimationEvent_HideCoin()`
* `AnimationEvent_ReturnToIdle()`

Die Münze wird aber zusätzlich über das Bridge-Script abgesichert, damit sie außerhalb von `Pinch_IdleBreak` nicht sichtbar bleibt.

## Testing

`PinchAnimatorAutoTester` ist nur für Animator-Tests gedacht.

Im Play Mode:

* `1` = Idle
* `2` = Dialog
* `3` = IdleBreak + Coin
* `R` = Reset zu Startposition + Idle

Zusätzlich erscheint ein kleines Testfenster im Game View mit Buttons für alle Animationen.

Falls im Editor ein GUI-/Skin-Fehler durch das Testfenster auftritt, kann im Inspector testweise deaktiviert werden:

* `Show On Screen Tester`

Für normales Gameplay deaktivieren oder entfernen:

* `PinchAnimatorAutoTester`

## Programmer Notes

Die Gameplay- oder Dialoglogik sollte Pinch über `PinchAnimatorBridge` ansteuern.

Movement, AI, Dialogsystem, Sounds, VFX und Interaktionslogik sind noch nicht implementiert.

Die Münze ist aktuell ein visuelles Prop und wird über `Coin Root` im Bridge-Script gesteuert.
