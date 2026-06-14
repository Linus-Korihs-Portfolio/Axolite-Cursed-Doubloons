# README_Enemy1

## Zweck

Enemy 1 ist ein animiertes Enemy-Prefab mit vorbereitetem Animator Controller und Bridge-Script für Gameplay-Anbindung.

## Dateien

* Prefab: `PF_Enemy1`
* Animator Controller: `AnCtrl_Enemy1`
* Scripts:

  * `Enemy1AnimatorBridge.cs`
  * `Enemy1AnimatorAutoTester.cs` optional, nur Testing
  * `Enemy1AnimationEvents.cs`

## Animator States

* `E1_Idle`
* `E1_Walk`
* `E1_MainAttack`
* `E1_LungeAttack`
* `E1_IdleBreak`
* `E1_Death`

`E1_Idle` ist der Default State.

## Animator Parameter

* `Speed` — Float
  Steuert Idle/Walk.

* `MainAttack` — Trigger
  Startet normale Attacke.

* `LungeAttack` — Trigger
  Startet Lunge-Attacke.

* `IdleBreak` — Trigger
  Startet optionale Idle-Unterbrechung.

* `IsDead` — Bool
  Startet Death-State.

## Transition-Logik

* `E1_Idle → E1_Walk`
  Condition: `Speed > 0.1`

* `E1_Walk → E1_Idle`
  Condition: `Speed < 0.1`

* `E1_MainAttack → E1_Idle`
  über Exit Time, keine Condition

* `E1_LungeAttack → E1_Idle`
  über Exit Time, keine Condition

* `E1_IdleBreak → E1_Idle`
  über Exit Time, keine Condition

* `Any State → E1_Death`
  Condition: `IsDead = true`

Death hat keine Rücktransition.

## Clip Settings

Loop Time ON:

* `E1_Idle`
* `E1_Walk`

Loop Time OFF:

* `E1_MainAttack`
* `E1_LungeAttack`
* `E1_IdleBreak`
* `E1_Death`

## Bridge-Script

`Enemy1AnimatorBridge` stellt folgende Methoden bereit:

* `SetSpeed(float speed)`
* `PlayMainAttack()`
* `PlayLungeAttack()`
* `PlayIdleBreak()`
* `SetDead(bool isDead)`
* `ResetToIdle()`

`ResetToIdle()` ist primär für Tests gedacht.

## Animation Events

`Enemy1AnimationEvents` enthält vorbereitete Event-Methoden für spätere Gameplay-Anbindung, z. B. Hit-Frames, Footsteps oder Death-Ende.

Aktuell dienen sie hauptsächlich als Debug-/Timing-Markierungen.

## Testing

`Enemy1AnimatorAutoTester` ist nur für Animator-Tests gedacht.

Empfohlene Testwerte:

* `Idle Wait`: 1.5
* `Walk Duration`: 2.0
* `Attack Wait`: 2.0
* `Lunge Wait`: 2.0
* `Idle Break Wait`: 3.0–5.0
* `Death Wait`: 2.5
* `Walk Speed`: 1.0

Für normales Gameplay deaktivieren:

* `Auto Run On Start`
* `Loop Test`

## Programmer Notes

Die Gameplay-Logik sollte Enemy 1 über `Enemy1AnimatorBridge` ansteuern.
Movement, AI, Damage, Hitboxen, Sounds und VFX sind noch nicht implementiert.
