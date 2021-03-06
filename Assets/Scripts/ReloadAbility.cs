﻿using UnityEngine;

[CreateAssetMenu(menuName = "Abilities/Reload")]
public class ReloadAbility : Ability {

    protected override bool SelectActionImpl(Entity entity) {
        if (entity.ammo == entity.gun.maxAmmo) {
            return false;
        }
        return true;
    }

    public override void TriggerAction(Entity entity) {
        entity.ammo = entity.gun.maxAmmo;
        //Pretty fire animations and such

        base.TriggerAction(entity);
    }
}
