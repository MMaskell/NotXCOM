﻿using UnityEngine;

public class HumanTeam : Team {

    bool turnActive = false;
    EntityController currentEntity;

    //Temporarily use the same entity repeatedly
    public GameObject entityPrefab;

    public Vector3Int[] spawnPositions = {
        new Vector3Int(5, 0, 5),
        new Vector3Int(6, 0, 5),
        new Vector3Int(5, 0, 6),
        new Vector3Int(6, 0, 6),
    };

    public override void EntityClicked(EntityController entity) {
        if (turnActive && !entity.actionsSpent) {
            //Make entity visibly selected (update hud, actions, etc)
            currentEntity = entity;
            Controller.entitySelect.transform.position = entity.GridPos;
            Controller.entitySelect.SetActive(true);
        }
        Controller.EntityClicked(entity);
    }

    public override void OnTurnStart() {
        turnActive = true;
        foreach (EntityController ent in entities) {
            ent.actionsSpent = false;
        }
        //Update UI stuff
    }

    public override void PopulateEntities() {
        for (int i = 0; i < spawnPositions.Length; i++) {
            GameObject newEnt = Object.Instantiate(entityPrefab, spawnPositions[i], Quaternion.identity);
            Controller.entities.Add(newEnt);
            entities.Add(newEnt.GetComponent<EntityController>());
            newEnt.GetComponent<EntityController>().team = this;
        }
    }

    public override void TileClicked(TileController tile) {
        if (turnActive) {
            if (currentEntity != null) {
                currentEntity.FollowPath(Controller.FindPath(currentEntity.GridPos, tile.gridPos));
                if(currentEntity.actionsSpent) {
                    currentEntity = null;
                    Controller.entitySelect.SetActive(false);
                }
                CheckActionsLeft();
            }
        }
    }

    public override void OnTurnEnd() {
        currentEntity = null;
    }

    public override void Update() {
        if(Input.GetKeyDown(KeyCode.End)) {
            Controller.NextTurn();
        }
    }

    void CheckActionsLeft() {
        foreach (EntityController ent in entities) {
            if(!ent.actionsSpent) {
                return;
            }
        }
        Controller.NextTurn();
    }

    public override void EnemyClicked(EntityController entity) {
        Debug.Log("Enemy clicked");
    }
}
