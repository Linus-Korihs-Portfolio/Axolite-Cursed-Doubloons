# README_Item

## Item: Chest / Opening Animation

Dieses Item ist eine animierte Truhe mit einem vorbereiteten Animator Controller.

## Dateien

* Prefab: `PF_Item`
* Animator Controller: `AnCtrl_Item`
* Animation: `Item_OpeningChest`
* Scripts:

  * `ItemAnimatorBridge.cs`
  * `ItemAnimatorAutoTester.cs`
  * `ItemAnimationEvents.cs`

## Animator-Aufbau

Empfohlener Aufbau:

`Entry → Item_Closed → Item_OpeningChest`

`Item_Closed` ist der Default State.
`Item_OpeningChest` spielt die Öffnungsanimation ab.

Falls kein eigener Closed-Clip existiert, kann `Item_Closed` ein Empty State sein oder den ersten Frame der Opening-Animation mit Speed `0` nutzen.

## Animator-Parameter

* `Open` — Trigger
  Startet die Öffnungsanimation.

* `Reset` — Trigger
  Setzt die Truhe für Tests bzw. erneutes Abspielen zurück in den geschlossenen Zustand.

## Transitionen

### Item_Closed → Item_OpeningChest

* Condition: `Open`
* Has Exit Time: Off
* Transition Duration: ca. `0.05–0.10`
* Loop Time der Opening-Animation: Off

### Item_OpeningChest → Item_Closed

* Condition: `Reset`
* Has Exit Time: Off
* Transition Duration: ca. `0.05–0.10`

## Bridge-Script

`ItemAnimatorBridge` stellt Methoden für die spätere Gameplay-Logik bereit:

* `OpenItem()`
  Löst `Open` aus.

* `ResetToClosed()`
  Löst `Reset` aus bzw. setzt die Truhe zurück.

* `ForceOpeningAnimation()`
  Spielt die Opening-Animation direkt ab.

