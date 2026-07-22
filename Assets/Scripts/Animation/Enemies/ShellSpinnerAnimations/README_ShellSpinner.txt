# README_ShellSpinner

## Zweck

Enemy 2 ist ein animiertes Enemy-Prefab mit vorbereitetem Animator Controller, Projectile-/Spin-Attack-Sequenzen und Bridge-Script für Gameplay-Anbindung.

## Dateien

* Prefab: `PF_Enemy2`
* Animator Controller: `AnCtrl_Enemy2`
* Scripts:

  * `Enemy2AnimatorBridge.cs`
  * `Enemy2AnimatorAutoTester.cs` optional, nur Testing
  * `Enemy2AnimationEvents.cs`

## Animator States

Basic:

* `E2_Idle`
* `E2_Walk`
* `E2_IdleBreak`
* `E2_Death`

ProjectileAttack:

* `E2_ProjectileAttack_1_3`
* `E2_ProjectileAttack_2_3`
* `E2_ProjectileAttack_3_3`

SpinAttack:

* `E2_SpinAttack_1_4`
* `E2_SpinAttack_2_4`
* `E2_SpinAttack_3_4`
* `E2_SpinAttack_4_4`

`E2_Idle` ist der Default State.

## Animator Parameter

* `Speed` — Float
  Steuert Idle/Walk.

* `ProjectileAttack` — Trigger
  Startet die ProjectileAttack-Sequenz.

* `SpinAttack` — Trigger
  Startet die SpinAttack-Sequenz.

* `IdleBreak` — Trigger
  Startet optionale Idle-Unterbrechung.

* `IsDead` — Bool
  Startet Death-State.

* `NeedsTurn` — Bool
  Entscheidet, ob nach `E2_ProjectileAttack_2_3` der kurze Turn-/Walk-Part `E2_ProjectileAttack_3_3` abgespielt wird.

* `ContinueShooting` — Bool
  Optional: Wenn aktiv, kann nach dem Turn-/Walk-Part erneut geschossen werden.

* `ContinueSpinning` — Bool
  Verlängert die SpinAttack, indem `E2_SpinAttack_2_4` länger gelooped wird.

## Transition-Logik

### Movement

* `E2_Idle → E2_Walk`
  Condition: `Speed > 0.1`

* `E2_Walk → E2_Idle`
  Condition: `Speed < 0.1`

### ProjectileAttack

* `E2_ProjectileAttack_1_3 → E2_ProjectileAttack_2_3`
  über Exit Time, keine Condition

* `E2_ProjectileAttack_2_3 → E2_Idle`
  Condition: `NeedsTurn = false`

* `E2_ProjectileAttack_2_3 → E2_ProjectileAttack_3_3`
  Condition: `NeedsTurn = true`

* `E2_ProjectileAttack_3_3 → E2_Idle`
  Condition: `ContinueShooting = false`

* `E2_ProjectileAttack_3_3 → E2_ProjectileAttack_2_3`
  optional, Condition: `ContinueShooting = true`

`E2_ProjectileAttack_3_3` ist kein Pflichtteil der Attacke. Er wird nur genutzt, wenn der Enemy wegen Target-Bewegung seine Richtung korrigieren muss.

### SpinAttack

* `E2_SpinAttack_1_4 → E2_SpinAttack_2_4`
  über Exit Time, keine Condition

* `E2_SpinAttack_2_4 → E2_SpinAttack_3_4`
  Condition: `ContinueSpinning = false`

* `E2_SpinAttack_3_4 → E2_SpinAttack_4_4`
  über Exit Time, keine Condition

* `E2_SpinAttack_4_4 → E2_Idle`
  über Exit Time, keine Condition

`E2_SpinAttack_2_4` kann über `ContinueSpinning = true` länger gehalten/gelooped werden. Sobald `ContinueSpinning = false` ist, läuft die Sequenz weiter zu `E2_SpinAttack_3_4`.

### Sonstige

* `E2_IdleBreak → E2_Idle`
  über Exit Time, keine Condition

* `Any State → E2_Death`
  Condition: `IsDead = true`

Death hat keine Rücktransition.

## Clip Settings

Loop Time ON:

* `E2_Idle`
* `E2_Walk`
* `E2_ProjectileAttack_2_3`
* `E2_SpinAttack_2_4`

Loop Time OFF:

* `E2_IdleBreak`
* `E2_Death`
* `E2_ProjectileAttack_1_3`
* `E2_ProjectileAttack_3_3`
* `E2_SpinAttack_1_4`
* `E2_SpinAttack_3_4`
* `E2_SpinAttack_4_4`

## Bridge-Script

`Enemy2AnimatorBridge` stellt folgende Methoden bereit:

* `SetSpeed(float speed)`
* `PlayProjectileAttack(bool needsTurn)`
* `PlayProjectileAttack(bool needsTurn, bool continueShooting)`
* `PlaySpinAttack()`
* `PlaySpinAttack(bool continueSpinning)`
* `StopSpinning()`
* `PlayIdleBreak()`
* `SetNeedsTurn(bool needsTurn)`
* `SetContinueShooting(bool continueShooting)`
* `SetContinueSpinning(bool continueSpinning)`
* `SetDead(bool isDead)`
* `ResetToIdle()`

`ResetToIdle()` ist primär für Tests gedacht.

## Animation Events

`Enemy1AnimationEvents` enthält vorbereitete Event-Methoden für spätere Gameplay-Anbindung, z. B. Hit-Frames, Footsteps oder Death-Ende.

Aktuell dienen sie hauptsächlich als Debug-/Timing-Markierungen.

## Testing

`Enemy2AnimatorAutoTester` ist nur für Animator-Tests gedacht.

Es kann testen:

* Idle
* Walk
* ProjectileAttack ohne Turn
* ProjectileAttack mit Turn
* ProjectileAttack mit Turn + erneutem Schuss
* normale SpinAttack
* verlängerte SpinAttack über `ContinueSpinning`
* IdleBreak
* Death

Für normales Gameplay deaktivieren:

* `Auto Run On Start`
* `Loop Test`

## Programmer Notes

Die Gameplay-Logik sollte Enemy 2 über `Enemy2AnimatorBridge` ansteuern.

Beispiele:

* `PlayProjectileAttack(false, false)`
  normaler Schuss ohne Richtungswechsel

* `PlayProjectileAttack(true, false)`
  Schuss mit anschließendem Turn-/Walk-Part

* `PlayProjectileAttack(true, true)`
  Schuss, Turn-/Walk-Part, danach erneuter Schuss

* `PlaySpinAttack(false)`
  normale SpinAttack

* `PlaySpinAttack(true)`
  verlängerte SpinAttack

* `StopSpinning()`
  beendet die verlängerte Spin-Phase und lässt die SpinAttack weiterlaufen

Movement, AI, Damage, Projektil-Spawn, Hitboxen, Sounds und VFX sind noch nicht implementiert.
