# README_ElderKoi

## Zweck

Elder Koi ist ein animiertes NPC-Prefab mit vorbereitetem Animator Controller und Bridge-Script für Dialog- und Idle-Animationen.

Elder Koi besitzt mehrere Dialogbewegungen, die später beim Reden zufällig abgespielt werden sollen.

## Dateien

* Prefab: `PF_ElderKoi`
* Animator Controller: `AnCtrl_ElderKoi`
* Scripts:

  * `ElderKoiAnimatorBridge.cs`
  * `ElderKoiAnimatorAutoTester.cs` optional, nur Testing
  * `ElderKoiAnimationEvents.cs`

## Animator States

* `ElderKoi_Idle`
* `ElderKoi_IdleBreak`
* `ElderKoi_V1Dialog`
* `ElderKoi_V2Dialog`
* `ElderKoi_V3Dialog`

`ElderKoi_Idle` ist der Default State.

## Animator Parameter

* `IdleBreak` — Trigger
  Startet die optionale Idle-Unterbrechung.

* `Dialog` — Trigger
  Ist aktuell noch im Animator vorgesehen, funktioniert in der momentanen Testkonfiguration aber nicht zuverlässig.

* `DialogVariant` — Int
  Gibt an, welche Dialoganimation abgespielt werden soll:

  * `1` = V1 Dialog
  * `2` = V2 Dialog
  * `3` = V3 Dialog

Der `Dialog`-Trigger bleibt vorerst zur Sicherheit im Animator, falls er später doch für die finale Dialoglogik benötigt wird.

Ein `Speed`-Parameter wird nicht benötigt, da Elder Koi kein laufender Enemy ist.

## Transition-Logik

* `ElderKoi_IdleBreak → ElderKoi_Idle`
  über Exit Time, keine Condition

* `ElderKoi_V1Dialog → ElderKoi_Idle`
  über Exit Time, keine Condition

* `ElderKoi_V2Dialog → ElderKoi_Idle`
  über Exit Time, keine Condition

* `ElderKoi_V3Dialog → ElderKoi_Idle`
  über Exit Time, keine Condition

Die Dialoganimationen werden aktuell über das Bridge-Script getestet und können dort direkt oder random abgespielt werden.

## Clip Settings

Loop Time ON:

* `ElderKoi_Idle`

Loop Time OFF:

* `ElderKoi_IdleBreak`
* `ElderKoi_V1Dialog`
* `ElderKoi_V2Dialog`
* `ElderKoi_V3Dialog`

## Bridge-Script

`ElderKoiAnimatorBridge` stellt folgende Methoden bereit:

* `PlayIdle()`
* `PlayIdleBreak()`
* `PlayRandomDialog()`
* `PlayV1Dialog()`
* `PlayV2Dialog()`
* `PlayV3Dialog()`
* `ResetToIdle()`
* `ResetToStartAndIdle()`

`PlayRandomDialog()` wählt zufällig zwischen V1, V2 und V3.

`ResetToStartAndIdle()` ist primär für Tests gedacht.

## Animation Events

Für Elder Koi gibt es aktuell erstmal keine benötigten Animation Events.

`ElderKoiAnimationEvents.cs` bleibt trotzdem vorbereitet im Projekt, falls später Timing-Events für Dialog, Sound, VFX oder Gameplay benötigt werden.

## Testing

`ElderKoiAnimatorAutoTester` ist nur für Animator-Tests gedacht.

Im Play Mode:

* `1` = Idle
* `2` = Random Dialog
* `3` = IdleBreak
* `4` = Dialog V1
* `5` = Dialog V2
* `6` = Dialog V3
* `R` = Reset zu Startposition + Idle

Zusätzlich erscheint ein kleines Testfenster im Game View mit Buttons für alle Animationen.

Für normales Gameplay deaktivieren oder entfernen:

* `ElderKoiAnimatorAutoTester`

## Programmer Notes

Die Gameplay- oder Dialoglogik sollte Elder Koi über `ElderKoiAnimatorBridge` ansteuern.

Der `Dialog`-Trigger funktioniert aktuell nicht zuverlässig, bleibt aber vorsichtshalber im Animator, damit er später bei Bedarf wieder genutzt oder final angebunden werden kann.

Movement, AI, Dialogsystem, Sounds, VFX und Interaktionslogik sind noch nicht implementiert.
