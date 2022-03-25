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

namespace ExamplePlugin
{
	//This is an example plugin that can be put in BepInEx/plugins/ExamplePlugin/ExamplePlugin.dll to test out.
    //It's a small plugin that adds a relatively simple item to the game, and gives you that item whenever you press F2.

    //This attribute specifies that we have a dependency on R2API, as we're using it to add our item to the game.
    //You don't need this if you're not using R2API in your plugin, it's just to tell BepInEx to initialize R2API before this plugin so it's safe to use R2API.
    [BepInDependency(R2API.R2API.PluginGUID)]
	
	//This attribute is required, and lists metadata for your plugin.
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
	
	//We will be using 3 modules from R2API: ItemAPI to add our item, ItemDropAPI to have our item drop ingame, and LanguageAPI to add our language tokens.
    [R2APISubmoduleDependency(nameof(ItemAPI), nameof(ItemDropAPI), nameof(LanguageAPI))]
	
	//This is the main declaration of our plugin class. BepInEx searches for all classes inheriting from BaseUnityPlugin to initialize on startup.
    //BaseUnityPlugin itself inherits from MonoBehaviour, so you can use this as a reference for what you can declare and use in your plugin class: https://docs.unity3d.com/ScriptReference/MonoBehaviour.html
    public class ScavengersForever : BaseUnityPlugin
	{
        //The Plugin GUID should be a unique ID for this plugin, which is human readable (as it is used in places like the config).
        //If we see this PluginGUID as it is on thunderstore, we will deprecate this mod. Change the PluginAuthor and the PluginName !
        public const string PluginGUID = PluginAuthor + "." + PluginName;
        public const string PluginAuthor = "AuthorName";
        public const string PluginName = "ExamplePlugin";
        public const string PluginVersion = "1.0.0";

		//We need our item definition to persist through our functions, and therefore make it a class field.
        private static ItemDef myItemDef;

		//The Awake() method is run at the very start when the game is initialized.
		public void Awake()
		{
			//Init our logging class so that we can properly log for debugging
			Log.Init(Logger);
			On.RoR2.CombatDirector.AttemptSpawnOnTarget += (orig, self, spawnTarget, placementMode) =>
			{
				return AttemptSpawnOnTarget(self, spawnTarget, placementMode);
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
					self.directorCore.TrySpawnObject(new DirectorSpawnRequest(spawnCard, placementRule, self.rng));
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
						//healthComponent.body.maxHealth = 0.000001f;
						//healthComponent.body.maxShield = 0f;
						healthComponent.Networkhealth = 0.00000000001f;
						healthComponent.Networkshield = -1000000000f;
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
			if (self.spawnCountInCurrentWave >= self.maximumNumberToSpawnBeforeSkipping)
			{
				self.spawnCountInCurrentWave = 0;
				if (CombatDirector.cvDirectorCombatEnableInternalLogs.value)
				{
					Debug.LogFormat("Spawn count has hit the max ({0}/{1}). Aborting spawn.", new object[]
					{
						self.spawnCountInCurrentWave,
						self.maximumNumberToSpawnBeforeSkipping
					});
				}
				return false;
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
			if (!self.currentMonsterCard.CardIsValid())
			{
				if (CombatDirector.cvDirectorCombatEnableInternalLogs.value)
				{
					Debug.LogFormat("Spawn card {0} is invalid, aborting spawn.", new object[]
					{
						self.currentMonsterCard.spawnCard
					});
				}
				return false;
			}
			if (self.monsterCredit < (float)dc.monsterCostThatMayOrMayNotBeElite)
			{
				if (CombatDirector.cvDirectorCombatEnableInternalLogs.value)
				{
					Debug.LogFormat("Spawn card {0} is too expensive, aborting spawn.", new object[]
					{
						self.currentMonsterCard.spawnCard
					});
				}
				return false;
			}
			if (self.skipSpawnIfTooCheap && (float)(num * self.maximumNumberToSpawnBeforeSkipping) < self.monsterCredit)
			{
				if (CombatDirector.cvDirectorCombatEnableInternalLogs.value)
				{
					Debug.LogFormat("Card {0} seems too cheap ({1}/{2}). Comparing against most expensive possible ({3})", new object[]
					{
						self.currentMonsterCard.spawnCard,
						dc.monsterCostThatMayOrMayNotBeElite * self.maximumNumberToSpawnBeforeSkipping,
						self.monsterCredit,
						self.mostExpensiveMonsterCostInDeck
					});
				}
				if (self.mostExpensiveMonsterCostInDeck > dc.monsterCostThatMayOrMayNotBeElite)
				{
					if (CombatDirector.cvDirectorCombatEnableInternalLogs.value)
					{
						Debug.LogFormat("Spawn card {0} is too cheap, aborting spawn.", new object[]
						{
							self.currentMonsterCard.spawnCard
						});
					}
					return false;
				}
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
