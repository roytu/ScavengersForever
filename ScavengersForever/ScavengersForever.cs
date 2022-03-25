using BepInEx;
using R2API;
using R2API.Utils;
using RoR2;
using UnityEngine;
using System;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using RoR2.ConVar;
using RoR2.Navigation;
using RoR2.CharacterAI;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;
using UnityEngine.Serialization;

namespace ScavengersForever
{
	//This is an example plugin that can be put in BepInEx/plugins/ScavengersForever/ScavengersForever.dll to test out.
    //It's a small plugin that adds a relatively simple item to the game, and gives you that item whenever you press F2.

    //This attribute specifies that we have a dependency on R2API, as we're using it to add our item to the game.
    //You don't need this if you're not using R2API in your plugin, it's just to tell BepInEx to initialize R2API before this plugin so it's safe to use R2API.
    [BepInDependency(R2API.R2API.PluginGUID)]
	
	//This attribute is required, and lists metadata for your plugin.
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
	
	//We will be using 3 modules from R2API: ItemAPI to add our item, ItemDropAPI to have our item drop ingame, and LanguageAPI to add our language tokens.
    [R2APISubmoduleDependency(nameof(ItemAPI), nameof(LanguageAPI))]
	
	//This is the main declaration of our plugin class. BepInEx searches for all classes inheriting from BaseUnityPlugin to initialize on startup.
    //BaseUnityPlugin itself inherits from MonoBehaviour, so you can use this as a reference for what you can declare and use in your plugin class: https://docs.unity3d.com/ScriptReference/MonoBehaviour.html
    public class ScavengersForever : BaseUnityPlugin
	{
        //The Plugin GUID should be a unique ID for this plugin, which is human readable (as it is used in places like the config).
        //If we see this PluginGUID as it is on thunderstore, we will deprecate this mod. Change the PluginAuthor and the PluginName !
        public const string PluginGUID = PluginAuthor + "." + PluginName;
        public const string PluginAuthor = "roytu";
        public const string PluginName = "ScavengersForever";
        public const string PluginVersion = "0.0.4";

		//We need our item definition to persist through our functions, and therefore make it a class field.
        private static ItemDef myItemDef;

		//The Awake() method is run at the very start when the game is initialized.
		public void Awake()
		{
			//Init our logging class so that we can properly log for debugging
			Log.Init(Logger);

			On.RoR2.Run.Start += (orig, self) =>
			{
				orig(self);
				PlayerCharacterMasterController.instances[0].master.inventory.GiveRandomEquipment();
				PlayerCharacterMasterController.instances[0].master.inventory.GiveRandomItems(20, true, true);
			};

			On.RoR2.CombatDirector.AttemptSpawnOnTarget += (orig, self, spawnTarget, placementMode) =>
			{
				return AttemptSpawnOnTarget(self, spawnTarget, placementMode);
			};

			On.RoR2.CombatDirector.Simulate += (orig, self, deltaTime) =>
			{
				if (self.targetPlayers)
				{
					self.playerRetargetTimer -= deltaTime;
					if (self.playerRetargetTimer <= 0f)
					{
						self.playerRetargetTimer = self.rng.RangeFloat(1f, 10f);
						self.PickPlayerAsSpawnTarget();
					}
				}
				self.monsterSpawnTimer -= deltaTime;
				if (self.monsterSpawnTimer <= 0f)
				{
					if (self.AttemptSpawnOnTarget(self.currentSpawnTarget ? self.currentSpawnTarget.transform : null, DirectorPlacementRule.PlacementMode.Random))
					{
						if (self.shouldSpawnOneWave)
						{
							Debug.Log("CombatDirector hasStartedwave = true");
							self.hasStartedWave = true;
						}
						self.monsterSpawnTimer += self.rng.RangeFloat(self.minSeriesSpawnInterval, self.maxSeriesSpawnInterval);
						return;
					}
					self.monsterSpawnTimer += self.rng.RangeFloat(self.minRerollSpawnInterval, self.maxRerollSpawnInterval) / 3f;  // Spawn faster
					if (self.resetMonsterCardIfFailed)
					{
						self.currentMonsterCard = null;
					}
					if (self.shouldSpawnOneWave && self.hasStartedWave)
					{
						Debug.Log("CombatDirector wave complete");
						base.enabled = false;
						return;
					}
				}
			};

			/*
			On.RoR2.SceneDirector.SelectCard += (orig, self, deck, maxCost) =>
			{
				return Resources.Load<SpawnCard>("spawncards/interactablespawncard/iscshrinerestack");
			};
			*/

			On.RoR2.SceneDirector.PopulateScene += (orig, self) =>
			{
				DirectorPlacementRule placementRule = new DirectorPlacementRule
				{
					placementMode = DirectorPlacementRule.PlacementMode.Random
				};
				SpawnCard spawnCard = Resources.Load<SpawnCard>("spawncards/interactablespawncard/iscshrinerestack");
				for (int i = 0; i < 20; i++)
				{
					DirectorCore.instance.TrySpawnObject(new DirectorSpawnRequest(spawnCard, placementRule, self.rng));
				}

				orig(self);
			};
			/*
			On.RoR2.DirectorCore.TrySpawnObject += (orig, self, directorSpawnRequest) =>
			{
				
				directorSpawnRequest.spawnCard = Resources.Load<SpawnCard>("spawncards/interactablespawncard/iscshrinerestack");
				return orig(self, directorSpawnRequest);
			};
			*/

			On.EntityStates.ScavMonster.Death.OnEnter += (orig, self) =>
			{
				orig(self);
				if (NetworkServer.active)
				{
					CharacterMaster characterMaster = self.characterBody ? self.characterBody.master : null;
					if (characterMaster)
					{
						self.shouldDropPack = true;
						Stage.instance.scavPackDroppedServer = true;
					}
				}
			};

			On.RoR2.CharacterMaster.OnBodyStart += (orig, self, body) =>
			{
				orig(self, body);
				OnBodyStart(self, body);
			};
		}

		public void OnBodyStart(RoR2.CharacterMaster self, CharacterBody body)
        {
			if (NetworkServer.active)
			{
				HealthComponent healthComponent = body.healthComponent;
				if (healthComponent)
				{
					if (self.teamIndex == TeamIndex.Monster)
					{
						//healthComponent.body.maxHealth *= 0.0001f;
						//healthComponent.body.maxShield = 0f;
						healthComponent.Networkhealth *= 0.0001f;
						healthComponent.Networkshield = -1000f;
					}
				}
			}
		}

		private class DisplayClass
        {
			public RoR2.CombatDirector combatDirector;
			public float monsterCostThatMayOrMayNotBeElite;
			public RoR2.CombatDirector.EliteTierDef eliteTier;
			public RoR2.EliteDef eliteDef;

			public void OnCardSpawned(SpawnCard.SpawnResult result)
            {

            }
        }

		private bool AttemptSpawnOnTarget(RoR2.CombatDirector self, Transform spawnTarget, DirectorPlacementRule.PlacementMode placementMode = DirectorPlacementRule.PlacementMode.Approximate)
		{
			DisplayClass dc = new DisplayClass();
			dc.combatDirector = self;
			if (self.currentMonsterCard == null)
			{
				if (CombatDirector.cvDirectorCombatEnableInternalLogs.value)
				{
					Debug.Log("Current monster card is null, pick new one.");
				}
				self.PrepareNewMonsterWave(self.finalMonsterCardsSelection.Evaluate(self.rng.nextNormalizedFloat));
			}

			int cost = self.currentMonsterCard.cost;
			dc.monsterCostThatMayOrMayNotBeElite = self.currentMonsterCard.cost;
			int num = self.currentMonsterCard.cost;
			dc.eliteTier = self.currentActiveEliteTier;
			dc.eliteDef = self.currentActiveEliteDef;
			num = (int)((float)dc.monsterCostThatMayOrMayNotBeElite * self.currentActiveEliteTier.costMultiplier);
			if ((float)num <= self.monsterCredit)
			{
				dc.monsterCostThatMayOrMayNotBeElite = num;
				dc.eliteTier = self.currentActiveEliteTier;
				dc.eliteDef = self.currentActiveEliteDef;
			}
			else
			{
				self.ResetEliteType();
			}
			
			SpawnCard spawnCard = self.currentMonsterCard.spawnCard;
			DirectorPlacementRule directorPlacementRule = new DirectorPlacementRule
			{
				placementMode = placementMode,
				spawnOnTarget = spawnTarget,
				preventOverhead = self.currentMonsterCard.preventOverhead
			};
			DirectorCore.GetMonsterSpawnDistance(self.currentMonsterCard.spawnDistance, out directorPlacementRule.minDistance, out directorPlacementRule.maxDistance);
			directorPlacementRule.minDistance *= self.spawnDistanceMultiplier;
			directorPlacementRule.maxDistance *= self.spawnDistanceMultiplier;

			spawnCard = Resources.Load<SpawnCard>("spawncards/characterspawncards/cscscav");

			DirectorSpawnRequest directorSpawnRequest = new DirectorSpawnRequest(spawnCard, directorPlacementRule, self.rng);
			directorSpawnRequest.ignoreTeamMemberLimit = self.ignoreTeamSizeLimit;
			directorSpawnRequest.teamIndexOverride = new TeamIndex?(self.teamIndex);
			//directorSpawnRequest.onSpawnedServer = new Action<SpawnCard.SpawnResult>(dc.OnCardSpawned);

			if (!DirectorCore.instance.TrySpawnObject(directorSpawnRequest))
			{
				Debug.LogFormat("Spawn card {0} failed to spawn. Aborting cost procedures.", new object[]
				{
					spawnCard
				});
				return false;
			}
			//self.monsterCredit -= (float)dc.monsterCostThatMayOrMayNotBeElite;
			self.spawnCountInCurrentWave++;
			return true;
		}
	}
}
