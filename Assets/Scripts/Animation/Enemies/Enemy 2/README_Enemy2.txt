# README_Enemy_02

## Übersicht

Dieses Asset enthält den vorbereiteten Animator-Aufbau für Enemy 2.

Enemy 2 besitzt normale Bewegungsanimationen sowie zwei größere Attack-Sequenzen, die aus mehreren einzelnen Animationsteilen bestehen:

* ProjectileAttack besteht aus 3 Teilen
* SpinAttack besteht aus 4 Teilen

Diese Teile gehören jeweils zu einer zusammenhängenden Animation und werden im Animator automatisch nacheinander abgespielt.

Ziel ist, dass Enemy 2 als animiertes Prefab technisch vorbereitet ist und später vom Programmer nur noch über klare Animator-Parameter oder das Bridge-Script angesteuert werden muss.

---

## Wichtige Dateien

### Prefab

PF_Enemy_02 !!(bei mir PF_Enemy2)!!

### Animator Controller

AC_Enemy_02 !!(bei mir AnCtrl_Enemy2)!!

### Scripts

Enemy2AnimatorBridge.cs
Enemy2AnimatorAutoTester.cs
Enemy2AnimationEvents.cs

---

## Animation Clips

Enemy 2 verwendet folgende Animation Clips:

### Basic

* E2_Idle
* E2_Walk
* E2_IdleBreak
* E2_Death

### ProjectileAttack-Sequenz

Diese drei Clips gehören zusammen und bilden eine vollständige Attacke:

* E2_ProjectileAttack_1_3
* E2_ProjectileAttack_2_3
* E2_ProjectileAttack_3_3

Ablauf:

E2_ProjectileAttack_1_3
→ E2_ProjectileAttack_2_3
→ E2_ProjectileAttack_3_3
→ zurück zu E2_Idle

### SpinAttack-Sequenz

Diese vier Clips gehören zusammen und bilden eine vollständige Attacke:

* E2_SpinAttack_1_4
* E2_SpinAttack_2_4
* E2_SpinAttack_3_4
* E2_SpinAttack_4_4

Ablauf:

E2_SpinAttack_1_4
→ E2_SpinAttack_2_4
→ E2_SpinAttack_3_4
→ E2_SpinAttack_4_4
→ zurück zu E2_Idle

---

## Animator-Parameter

Der Animator Controller verwendet folgende Parameter:

### Speed

Typ: Float

Verwendung:

* 0 = Idle
* größer als 0.1 = Walk

Dieser Parameter steuert den Wechsel zwischen Idle und Walk.

---

### ProjectileAttack

Typ: Trigger

Verwendung:

Startet die komplette ProjectileAttack-Sequenz.

Wichtig:

Nur der erste State der Sequenz wird durch den Trigger gestartet.

ProjectileAttack Trigger
→ E2_ProjectileAttack_1_3
→ E2_ProjectileAttack_2_3
→ E2_ProjectileAttack_3_3
→ E2_Idle

Die einzelnen Teile 2 und 3 werden nicht separat getriggert, sondern automatisch über Has Exit Time abgespielt.

---

### SpinAttack

Typ: Trigger

Verwendung:

Startet die komplette SpinAttack-Sequenz.

Wichtig:

Nur der erste State der Sequenz wird durch den Trigger gestartet.

SpinAttack Trigger
→ E2_SpinAttack_1_4
→ E2_SpinAttack_2_4
→ E2_SpinAttack_3_4
→ E2_SpinAttack_4_4
→ E2_Idle

Die einzelnen Teile 2, 3 und 4 werden nicht separat getriggert, sondern automatisch über Has Exit Time abgespielt.

---

### IdleBreak

Typ: Trigger

Verwendung:

Löst eine kurze Idle-Unterbrechung aus.

Beispiel:

* Zucken
* Umschauen
* kurze Charakterbewegung
* kleine Ambient-Animation

Ablauf:

E2_Idle
→ E2_IdleBreak
→ E2_Idle

Dieser Parameter ist optional und kann im Gameplay ignoriert werden, falls er nicht benötigt wird.

---

### IsDead

Typ: Bool

Verwendung:

* false = Enemy lebt
* true = Enemy wechselt in Death-State

Death ist als finaler Zustand gedacht.

Ablauf:

Any State
→ E2_Death

Von E2_Death sollte es normalerweise keine Rücktransition geben.

---

## Animator-State-Aufbau

Empfohlener Aufbau:

Entry
→ E2_Idle

E2_Idle
→ E2_Walk

E2_Walk
→ E2_Idle

E2_Idle
→ E2_IdleBreak
→ E2_Idle

Any State
→ E2_ProjectileAttack_1_3
→ E2_ProjectileAttack_2_3
→ E2_ProjectileAttack_3_3
→ E2_Idle

Any State
→ E2_SpinAttack_1_4
→ E2_SpinAttack_2_4
→ E2_SpinAttack_3_4
→ E2_SpinAttack_4_4
→ E2_Idle

Any State
→ E2_Death

---

## Transition-Regeln

### E2_Idle → E2_Walk

Condition:

* Speed greater than 0.1

Empfohlene Einstellungen:

* Has Exit Time: Off
* Transition Duration: ca. 0.10 bis 0.20
* Fixed Duration: On

---

### E2_Walk → E2_Idle

Condition:

* Speed less than 0.1

Empfohlene Einstellungen:

* Has Exit Time: Off
* Transition Duration: ca. 0.10 bis 0.20
* Fixed Duration: On

---

## ProjectileAttack-Transitions

### Any State → E2_ProjectileAttack_1_3

Condition:

* ProjectileAttack Trigger

Empfohlene Einstellungen:

* Has Exit Time: Off
* Transition Duration: ca. 0.05 bis 0.10
* Can Transition To Self: Off

---

### E2_ProjectileAttack_1_3 → E2_ProjectileAttack_2_3

Condition:

* keine

Empfohlene Einstellungen:

* Has Exit Time: On
* Exit Time: ca. 0.95 bis 1.00
* Transition Duration: ca. 0.02 bis 0.05
* Fixed Duration: On

---

### E2_ProjectileAttack_2_3 → E2_ProjectileAttack_3_3

Condition:

* keine

Empfohlene Einstellungen:

* Has Exit Time: On
* Exit Time: ca. 0.95 bis 1.00
* Transition Duration: ca. 0.02 bis 0.05
* Fixed Duration: On

---

### E2_ProjectileAttack_3_3 → E2_Idle

Condition:

* keine

Empfohlene Einstellungen:

* Has Exit Time: On
* Exit Time: ca. 0.90 bis 0.98
* Transition Duration: ca. 0.05 bis 0.15
* Fixed Duration: On

---

## SpinAttack-Transitions

### Any State → E2_SpinAttack_1_4

Condition:

* SpinAttack Trigger

Empfohlene Einstellungen:

* Has Exit Time: Off
* Transition Duration: ca. 0.05 bis 0.10
* Can Transition To Self: Off

---

### E2_SpinAttack_1_4 → E2_SpinAttack_2_4

Condition:

* keine

Empfohlene Einstellungen:

* Has Exit Time: On
* Exit Time: ca. 0.95 bis 1.00
* Transition Duration: ca. 0.02 bis 0.05
* Fixed Duration: On

---

### E2_SpinAttack_2_4 → E2_SpinAttack_3_4

Condition:

* keine

Empfohlene Einstellungen:

* Has Exit Time: On
* Exit Time: ca. 0.95 bis 1.00
* Transition Duration: ca. 0.02 bis 0.05
* Fixed Duration: On

---

### E2_SpinAttack_3_4 → E2_SpinAttack_4_4

Condition:

* keine

Empfohlene Einstellungen:

* Has Exit Time: On
* Exit Time: ca. 0.95 bis 1.00
* Transition Duration: ca. 0.02 bis 0.05
* Fixed Duration: On

---

### E2_SpinAttack_4_4 → E2_Idle

Condition:

* keine

Empfohlene Einstellungen:

* Has Exit Time: On
* Exit Time: ca. 0.90 bis 0.98
* Transition Duration: ca. 0.05 bis 0.15
* Fixed Duration: On

---

## IdleBreak-Transitions

### E2_Idle → E2_IdleBreak

Condition:

* IdleBreak Trigger

Empfohlene Einstellungen:

* Has Exit Time: Off
* Transition Duration: ca. 0.10

---

### E2_IdleBreak → E2_Idle

Condition:

* keine

Empfohlene Einstellungen:

* Has Exit Time: On
* Exit Time: ca. 0.90 bis 1.00
* Transition Duration: ca. 0.10

---

## Death-Transition

### Any State → E2_Death

Condition:

* IsDead equals true

Empfohlene Einstellungen:

* Has Exit Time: Off
* Transition Duration: ca. 0.05 bis 0.15
* Can Transition To Self: Off

Von E2_Death sollte es normalerweise keine Rücktransition geben.

Death soll von jedem Zustand erreichbar sein:

* Idle
* Walk
* IdleBreak
* ProjectileAttack
* SpinAttack

---

## Clip-Looping

Folgende Clips sollen loopen:

* E2_Idle
* E2_Walk

Bei diesen Clips im Animation Importer aktivieren:

* Loop Time: On
* Loop Pose: optional On, falls der Loop sichtbar ruckelt

Folgende Clips sollen nicht loopen:

* E2_IdleBreak
* E2_Death
* E2_ProjectileAttack_1_3
* E2_ProjectileAttack_2_3
* E2_ProjectileAttack_3_3
* E2_SpinAttack_1_4
* E2_SpinAttack_2_4
* E2_SpinAttack_3_4
* E2_SpinAttack_4_4

Bei diesen Clips:

* Loop Time: Off

Wichtig:

Die einzelnen Attack-Teile dürfen nicht loopen, da die Sequenz sonst hängen bleiben kann.

---

## Root Motion

Aktueller empfohlener Zustand:

* Apply Root Motion: Off

Die echte Bewegung des Enemys sollte später durch Gameplay-Code, AI, NavMesh oder ein Enemy-Movement-Script gesteuert werden.

Die Animationen sind hauptsächlich für die visuelle Bewegung gedacht.

Falls die SpinAttack oder ProjectileAttack später echte Positionsbewegung über Animation steuern soll, muss Root Motion bewusst vom Programmer geprüft werden.

---

## Scripts auf dem Prefab

Auf dem Root-Objekt des Enemy-2-Prefabs sollten folgende Komponenten liegen:

* Animator
* Enemy2AnimatorBridge
* Enemy2AnimatorAutoTester
* Enemy2AnimationEvents

Empfohlener Aufbau:

PF_Enemy_02

* Animator
* Enemy2AnimatorBridge
* Enemy2AnimatorAutoTester
* Enemy2AnimationEvents

---

## Enemy2AnimatorBridge

Dieses Script dient als Schnittstelle zwischen Gameplay-Code und Animator.

Es stellt einfache Methoden bereit, damit der Programmer die Animationen sauber ansteuern kann.

Wichtige Methoden:

### SetSpeed(float speed)

Steuert Idle/Walk über den Speed-Parameter.

Beispiel:

* SetSpeed(0f) = Idle
* SetSpeed(1f) = Walk

---

### PlayProjectileAttack()

Löst den ProjectileAttack-Trigger aus.

Dadurch wird die komplette ProjectileAttack-Sequenz abgespielt:

E2_ProjectileAttack_1_3
→ E2_ProjectileAttack_2_3
→ E2_ProjectileAttack_3_3
→ E2_Idle

---

### PlaySpinAttack()

Löst den SpinAttack-Trigger aus.

Dadurch wird die komplette SpinAttack-Sequenz abgespielt:

E2_SpinAttack_1_4
→ E2_SpinAttack_2_4
→ E2_SpinAttack_3_4
→ E2_SpinAttack_4_4
→ E2_Idle

---

### PlayIdleBreak()

Löst den optionalen IdleBreak-Trigger aus.

---

### SetDead(bool isDead)

Setzt den IsDead-Bool.

Beispiel:

* SetDead(true) = Death Animation
* SetDead(false) = nicht tot

---

### ResetToIdle()

Setzt den Enemy für Tests zurück.

Diese Methode:

* setzt Speed auf 0
* setzt IsDead auf false
* resettet die Trigger
* spielt E2_Idle ab

Wichtig:

ResetToIdle ist hauptsächlich für Tests gedacht und muss im echten Gameplay nicht zwingend verwendet werden.

---

## Enemy2AnimatorAutoTester

Dieses Script testet die Animationen automatisch nacheinander.

Ablauf:

1. Idle
2. Walk
3. zurück zu Idle
4. ProjectileAttack 1–3
5. SpinAttack 1–4
6. IdleBreak
7. Death

Wichtige Inspector-Optionen:

### Auto Run On Start

Wenn aktiv, startet der Test automatisch beim Start der Szene.

### Loop Test

Wenn aktiv, wird die komplette Testsequenz immer wiederholt.

Das looped nicht einzelne Animation Clips, sondern die gesamte Testreihenfolge.

Für einzelne Animationen wie Idle oder Walk muss weiterhin im Animation Importer Loop Time aktiviert sein.

### Projectile Attack Wait

Wartezeit für die komplette ProjectileAttack-Sequenz.

Empfohlener Startwert:

* 3.0 Sekunden

Falls die Sequenz länger ist, Wert erhöhen.

### Spin Attack Wait

Wartezeit für die komplette SpinAttack-Sequenz.

Empfohlener Startwert:

* 4.0 Sekunden

Falls die Sequenz länger ist, Wert erhöhen.

### Delay Between Loops

Wartezeit zwischen zwei vollständigen Testdurchläufen.

---

## Enemy2AnimationEvents

Dieses Script enthält vorbereitete Event-Methoden für Enemy 2.

Aktuelle Methoden:

### Allgemein

* OnFootstep()
* OnIdleBreakFinished()
* OnDeathFinished()

### ProjectileAttack

* OnProjectileAttackStart()
* OnProjectileAttackCharge()
* OnProjectileAttackShoot()
* OnProjectileAttackEnd()

### SpinAttack

* OnSpinAttackStart()
* OnSpinAttackHit()
* OnSpinAttackEnd()

Aktuell geben diese Methoden Debug.Log-Meldungen aus.

Der Programmer kann dort später echte Gameplay-Logik anschließen, z. B.:

* Projectile spawnen
* Damage aktivieren
* Hitbox aktivieren
* Sound abspielen
* VFX spawnen
* Enemy nach Death entfernen
* Loot droppen

---

## Animation Events

Optional können Animation Events gesetzt werden, um wichtige Timing-Punkte zu markieren.

Empfohlene Events:

### E2_ProjectileAttack_1_3

Optional:

* OnProjectileAttackStart
* OnProjectileAttackCharge

### E2_ProjectileAttack_2_3 oder E2_ProjectileAttack_3_3

Wichtig:

* OnProjectileAttackShoot

Dieses Event sollte auf den Frame gesetzt werden, an dem das Projektil wirklich erzeugt oder abgeschossen werden soll.

### E2_ProjectileAttack_3_3

Optional:

* OnProjectileAttackEnd

---

### E2_SpinAttack_1_4

Optional:

* OnSpinAttackStart

### E2_SpinAttack_2_4 oder E2_SpinAttack_3_4

Wichtig:

* OnSpinAttackHit

Dieses Event sollte auf einen oder mehrere Frames gesetzt werden, an denen die SpinAttack Schaden machen soll.

### E2_SpinAttack_4_4

Optional:

* OnSpinAttackEnd

---

### E2_Walk

Optional:

* OnFootstep

Kann bei sichtbarem Bodenkontakt gesetzt werden, falls später Footstep-Sounds oder Partikel ausgelöst werden sollen.

---

### E2_Death

Optional:

* OnDeathFinished

Kann auf den letzten Frame gesetzt werden, falls später nach der Death-Animation etwas passieren soll.

---


## Hinweise für Programming

Die Animationen können über Enemy2AnimatorBridge angesteuert werden.

Beispielhafte Nutzung:

SetSpeed(currentSpeed)

PlayProjectileAttack()

PlaySpinAttack()

PlayIdleBreak()

SetDead(true)

Die Scripts Enemy2AnimatorAutoTester und Enemy2AnimationEvents sind aktuell vor allem für Testing und Event-Vorbereitung gedacht.

Die eigentliche AI, Bewegung, Projektil-Logik, Schadensberechnung, Hitbox-Aktivierung und Death-Logik müssen später durch Gameplay-Code ergänzt werden.

Besonders wichtig:

ProjectileAttack besteht aus drei Clips, wird aber nur durch einen Trigger gestartet.

SpinAttack besteht aus vier Clips, wird aber nur durch einen Trigger gestartet.

Die einzelnen Teile der Attack-Sequenzen werden automatisch über Has Exit Time verbunden.

---

## Aktueller Status

Enemy 2 ist als animiertes Asset vorbereitet.

Animator, Parameter, States, Attack-Sequenzen und Testlogik sind so angelegt, dass der Programmer die Animationen über klare Methoden ansteuern kann.

Offene Punkte für Programming:

* echte Enemy-AI
* Movement/NavMesh
* Projektil-Spawn bei OnProjectileAttackShoot
* Damage/Hitbox bei OnSpinAttackHit
* Health-System
* Death-Handling im Gameplay
* Sound/VFX-Anbindung
* ggf. Root-Motion-Entscheidung für SpinAttack
