# README_Kelp_Player

## Zweck

Kelp ist der Player Character mit vorbereitetem Animator Controller und Bridge-Script für Gameplay-Anbindung.

## Dateien

* Prefab: `PF_Kelp` 
* Animator Controller: `AnCtrl_Kelp`
* Scripts:

  * `KelpAnimatorBridge.cs`
  * `KelpAnimatorAutoTester.cs` optional, nur Testing
  * `KelpAnimationEvents.cs`

## Animator States

Base:

* `Kelp_Idle`
* `Kelp_Walk`
* `Kelp_IdleBreak`

Actions:

* `Kelp_Punch`
* `Kelp_PunchWhileWalking`
* `Kelp_CallMinions`
* `Kelp_CallMinionsWhileWalking`
* `Kelp_OrderMinions`
* `Kelp_OrderMinionsWhileWalking`
* `Kelp_Dismiss`
* `Kelp_DismissWhileWalking`
* `Kelp_Dodge`
* `Kelp_InteractWithObject`
* `Kelp_Death`

`Kelp_Idle` ist der Default State.

## Animator Parameter

* `Speed` — Float
  Steuert Idle/Walk und entscheidet zwischen Standing- und Walking-Actions.

* `Punch` — Trigger

* `CallMinions` — Trigger

* `OrderMinions` — Trigger

* `Dismiss` — Trigger

* `Dodge` — Trigger

* `Interact` — Trigger

* `IdleBreak` — Trigger

* `IsDead` — Bool

## Transition-Logik

Movement:

* `Kelp_Idle → Kelp_Walk`
  Condition: `Speed > 0.2`

* `Kelp_Walk → Kelp_Idle`
  Condition: `Speed < 0.05`

Actions:

* Standing-Actions starten bei `Speed < 0.2`
* Walking-Actions starten bei `Speed > 0.2`

Beispiel:

* `Punch + Speed < 0.2` → `Kelp_Punch`
* `Punch + Speed > 0.2` → `Kelp_PunchWhileWalking`

Rückwege:

* Standing-Actions → `Kelp_Idle`
* Walking-Actions → `Kelp_Walk`
* `Kelp_Dodge → Kelp_Idle` bei `Speed < 0.05`
* `Kelp_Dodge → Kelp_Walk` bei `Speed > 0.2`

Death:

* `Any State → Kelp_Death`
* Condition: `IsDead = true`
* keine Rücktransition

## Clip Settings

Loop Time ON:

* `Kelp_Idle`
* `Kelp_Walk`

Loop Time OFF:

* alle Action-Animationen
* `Kelp_Dodge`
* `Kelp_InteractWithObject`
* `Kelp_Death`
* `Kelp_IdleBreak`

## Bridge-Script

`KelpAnimatorBridge` stellt folgende Methoden bereit:

* `SetSpeed(float speed)`
* `PlayPunch()`
* `PlayCallMinions()`
* `PlayOrderMinions()`
* `PlayDismiss()`
* `PlayDodge()`
* `PlayInteract()`
* `PlayIdleBreak()`
* `SetDead(bool isDead)`
* `ResetToIdle()`

## Animation Events

`KelpAnimationEvents` enthält vorbereitete Events für Timing/Gameplay:

* `OnFootstep()`
* `OnPunchHit()`
* `OnCallMinionsMoment()`
* `OnOrderMinionsMoment()`
* `OnDismissMinionsMoment()`
* `OnInteractMoment()`
* `OnDodgeStart()`
* `OnDodgeEnd()`
* `OnDeathFinished()`

Aktuell hauptsächlich Debug-/Timing-Markierungen.

## Testing

`KelpAnimatorAutoTester` ist nur für Animator-Tests gedacht.

Testet u. a.:

* Idle/Walk
* IdleBreak
* Standing- und Walking-Versionen der Actions
* Dodge
* Interact
* Death

Für normales Gameplay deaktivieren:

* `Auto Run On Start`
* `Loop Test`

## Programmer Notes

Die Gameplay-Logik sollte Kelp über `KelpAnimatorBridge` ansteuern.

Wichtig: Walking-Versionen werden nicht separat aufgerufen. Der Animator entscheidet anhand von `Speed`, ob Standing- oder Walking-Animation abgespielt wird.

Movement, Input, Minion-Logik, Damage, Interaktion, Sounds und VFX sind noch nicht implementiert.
