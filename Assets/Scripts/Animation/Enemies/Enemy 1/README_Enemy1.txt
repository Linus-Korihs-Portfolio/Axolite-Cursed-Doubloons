# README_Enemy_01

## Übersicht

Dieses Asset enthält den vorbereiteten Animator-Aufbau für Enemy 1.
Ziel ist, dass der Enemy als animiertes Prefab technisch vorbereitet ist und später vom Programmer nur noch über Scripts/AI/Game Logic angesteuert werden muss.

Das Asset enthält:

* Enemy-Modell
* Materialien und Texturen
* Animation Clips
* Animator Controller
* Enemy Prefab
* Animator-Bridge-Script
* optionales Tester-Script
* optionales Auto-Tester-Script
* optionale Animation Events

---

## Wichtige Dateien

### Prefab

PF_Enemy_01 !!(bei mir PF_Enemy1)!!

### Animator Controller

AC_Enemy_01 !!(bei mir AnCtrl_Enemy1)!!

### Animation Clips

* E1_Idle
* E1_Walk
* E1_MainAttack
* E1_LungeAttack
* E1_IdleBreak
* E1_Death

---

## Animator-Parameter

Der Animator Controller verwendet folgende Parameter:

### Speed

Typ: Float

Verwendung:

* 0 = Idle
* größer als 0.1 = Walk

Dieser Parameter steuert die Bewegung zwischen Idle und Walk.

---

### MainAttack

Typ: Trigger

Verwendung:

Löst die normale Angriffsanimation aus.

Animation:

* E1_MainAttack

---

### LungeAttack

Typ: Trigger

Verwendung:

Löst die Lunge-/Dash-/Stoßangriffsanimation aus.

Animation:

* E1_LungeAttack

---

### IdleBreak

Typ: Trigger

Verwendung:

Optionaler Trigger für eine kleine Idle-Unterbrechung, z. B. Zucken, Umschauen oder kurze Charakteranimation.

Animation:

* E1_IdleBreak

Falls diese Animation im Gameplay nicht benötigt wird, kann der Parameter ignoriert werden.

---

### IsDead

Typ: Bool

Verwendung:

* false = Enemy lebt
* true = Enemy wechselt in Death-State

Animation:

* E1_Death

Death ist als finaler Zustand gedacht und sollte normalerweise nicht automatisch zurück in Idle wechseln.

---

## Animator-State-Aufbau

Empfohlener Aufbau:

Entry
→ E1_Idle

E1_Idle
→ E1_Walk

E1_Walk
→ E1_Idle

Any State
→ E1_MainAttack

Any State
→ E1_LungeAttack

Any State
→ E1_Death

Optional:

E1_Idle
→ E1_IdleBreak
→ E1_Idle

---

## Transition-Regeln

### E1_Idle → E1_Walk

Condition:

* Speed greater than 0.1

Empfohlene Einstellungen:

* Has Exit Time: Off
* Transition Duration: ca. 0.10 bis 0.20
* Fixed Duration: On

---

### E1_Walk → E1_Idle

Condition:

* Speed less than 0.1

Empfohlene Einstellungen:

* Has Exit Time: Off
* Transition Duration: ca. 0.10 bis 0.20
* Fixed Duration: On

---

### Any State → E1_MainAttack

Condition:

* MainAttack Trigger

Empfohlene Einstellungen:

* Has Exit Time: Off
* Transition Duration: ca. 0.05 bis 0.10
* Can Transition To Self: Off

---

### E1_MainAttack → E1_Idle

Condition:

* keine

Empfohlene Einstellungen:

* Has Exit Time: On
* Exit Time: ca. 0.85 bis 0.95
* Transition Duration: ca. 0.05 bis 0.15

---

### Any State → E1_LungeAttack

Condition:

* LungeAttack Trigger

Empfohlene Einstellungen:

* Has Exit Time: Off
* Transition Duration: ca. 0.05 bis 0.10
* Can Transition To Self: Off

---

### E1_LungeAttack → E1_Idle

Condition:

* keine

Empfohlene Einstellungen:

* Has Exit Time: On
* Exit Time: ca. 0.85 bis 0.95
* Transition Duration: ca. 0.05 bis 0.15

---

### E1_Idle → E1_IdleBreak

Condition:

* IdleBreak Trigger

Empfohlene Einstellungen:

* Has Exit Time: Off
* Transition Duration: ca. 0.10

---

### E1_IdleBreak → E1_Idle

Condition:

* keine

Empfohlene Einstellungen:

* Has Exit Time: On
* Exit Time: ca. 0.90 bis 1.00
* Transition Duration: ca. 0.10

---

### Any State → E1_Death

Condition:

* IsDead equals true

Empfohlene Einstellungen:

* Has Exit Time: Off
* Transition Duration: ca. 0.05 bis 0.15
* Can Transition To Self: Off

Von E1_Death sollte es normalerweise keine Rücktransition geben.

---

## Clip-Looping

Folgende Clips sollen loopen:

* E1_Idle
* E1_Walk

Bei diesen Clips im Animation Importer aktivieren:

* Loop Time: On
* Loop Pose: optional On, falls der Loop sichtbar ruckelt

Folgende Clips sollen normalerweise nicht loopen:

* E1_MainAttack
* E1_LungeAttack
* E1_IdleBreak
* E1_Death

Bei diesen Clips:

* Loop Time: Off

---

## Root Motion

Aktueller empfohlener Zustand:

* Apply Root Motion: Off

Die echte Bewegung des Enemys sollte später durch Gameplay-Code, AI, NavMesh oder ein Enemy-Movement-Script gesteuert werden.

Die Animationen sind hauptsächlich für die visuelle Bewegung gedacht.

Falls die LungeAttack später echte Vorwärtsbewegung über die Animation steuern soll, muss Root Motion bewusst vom Programmer geprüft und aktiviert werden.

---

## Scripts auf dem Prefab

Auf dem Root-Objekt des Enemy-Prefabs sollten folgende Komponenten liegen:

* Animator
* EnemyAnimatorBridge
* EnemyAnimationEvents
* EnemyAnimatorAutoTester

Empfohlener Aufbau:

PF_Enemy_01

* Animator
* EnemyAnimatorBridge
* EnemyAnimationEvents
* EnemyAnimatorAutoTester 

---

## EnemyAnimatorBridge

Dieses Script dient als Schnittstelle zwischen Gameplay-Code und Animator.

Es stellt einfache Methoden bereit, damit der Programmer die Animationen sauber ansteuern kann.

Wichtige Methoden:

SetSpeed(float speed)

Steuert Idle/Walk über den Speed-Parameter.

Beispiel:

* SetSpeed(0f) = Idle
* SetSpeed(1f) = Walk

---

PlayMainAttack()

Löst den MainAttack-Trigger aus.

---

PlayLungeAttack()

Löst den LungeAttack-Trigger aus.

---

PlayIdleBreak()

Löst den optionalen IdleBreak-Trigger aus.

---

SetDead(bool isDead)

Setzt den IsDead-Bool.

Beispiel:

* SetDead(true) = Death Animation
* SetDead(false) = nicht tot

---

ResetToIdle()

Setzt den Enemy für Tests zurück.

Diese Methode sollte:

* Speed auf 0 setzen
* IsDead auf false setzen
* Attack-Trigger zurücksetzen
* den Idle-State neu starten

Wichtig: ResetToIdle ist hauptsächlich für Tests gedacht und muss im echten Gameplay nicht zwingend verwendet werden.

---


## EnemyAnimatorAutoTester

Dieses Script testet die Animationen automatisch nacheinander.

Ablauf:

1. Idle
2. Walk
3. zurück zu Idle
4. MainAttack
5. LungeAttack
6. IdleBreak
7. Death

Wichtige Inspector-Optionen:

### Auto Run On Start

Wenn aktiv, startet der Test automatisch beim Start der Szene.

### Loop Test

Wenn aktiv, wird die komplette Testsequenz immer wiederholt.

Das looped nicht einzelne Animation Clips, sondern die gesamte Testreihenfolge.

Für einzelne Animationen wie Idle oder Walk muss weiterhin im Animation Importer Loop Time aktiviert sein.

### Delay Between Loops

Wartezeit zwischen zwei vollständigen Testdurchläufen.

---

## Animation Events

Optional können Animation Events gesetzt werden, um wichtige Timing-Punkte zu markieren.

Empfohlene Events:

### E1_MainAttack

OnMainAttackHit

Soll auf den Frame gesetzt werden, an dem der Angriff tatsächlich treffen würde.

---

### E1_LungeAttack

OnLungeAttackStart

Soll am Anfang der Lunge-Bewegung gesetzt werden.

OnLungeAttackHit

Soll auf den Treffer-Frame gesetzt werden.

OnLungeAttackEnd

Soll am Ende der Lunge-Bewegung gesetzt werden.

---

### E1_Walk

OnFootstep

Optional bei sichtbarem Bodenkontakt setzen, falls später Sounds oder Partikel ausgelöst werden sollen.

---

### E1_Death

OnDeathFinished

Optional am letzten Frame der Death-Animation setzen.

---

## EnemyAnimationEvents

Dieses Script enthält leere bzw. vorbereitete Event-Methoden.

Aktuelle Methoden:

* OnMainAttackHit()
* OnLungeAttackStart()
* OnLungeAttackHit()
* OnLungeAttackEnd()
* OnFootstep()
* OnDeathFinished()

Aktuell geben diese Methoden nur Debug.Log-Meldungen aus.

Der Programmer kann dort später echte Gameplay-Logik anschließen, z. B.:

* Schaden auslösen
* Hitbox aktivieren
* Sound abspielen
* Partikel/VFX spawnen
* Enemy nach Death entfernen
* Loot droppen



## Hinweise für Programming

Die Animationen können über EnemyAnimatorBridge angesteuert werden.

Beispielhafte Nutzung:

SetSpeed(currentSpeed)

PlayMainAttack()

PlayLungeAttack()

SetDead(true)

Die Scripts EnemyAnimatorAutoTester sind nur Testwerkzeuge und nicht als finale Enemy-Logik gedacht.

Die eigentliche AI, Bewegung, Schadensberechnung, Hitbox-Aktivierung und Death-Logik müssen später durch Gameplay-Code ergänzt werden.

---

## Aktueller Status

Enemy 1 ist als animiertes Asset vorbereitet.

Animator, Parameter, States und Testlogik sind so angelegt, dass der Programmer die Animationen über klare Methoden ansteuern kann.

Offene Punkte für Programming:

* echte Enemy-AI
* Movement/NavMesh
* Angriffsdistanzen
* Hitboxen oder Damage-Zonen
* Health-System
* Death-Handling im Gameplay
* Sound/VFX-Anbindung
* ggf. Root-Motion-Entscheidung für LungeAttack
