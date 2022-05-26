using BepInEx;
using R2API;
using R2API.Utils;
using RoR2;
using UnityEngine;
using System;

using System;
using System.Collections.Generic;
using System.Linq;
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
        public const string PluginVersion = "0.0.6";

		//We need our item definition to persist through our functions, and therefore make it a class field.
        private static ItemDef myItemDef;

		public static int spawnCount = 0;
		public static DirectorPlacementRule.PlacementMode enemyPlacement = DirectorPlacementRule.PlacementMode.Random;

		//The Awake() method is run at the very start when the game is initialized.
		public void Awake()
		{
			//Init our logging class so that we can properly log for debugging
			Log.Init(Logger);

			On.RoR2.Run.Start += (orig, self) =>
			{
				orig(self);
				int playerCount = PlayerCharacterMasterController.instances.Count;
				for (int i = 0; i < playerCount; i++)
                {
					PlayerCharacterMasterController.instances[i].master.inventory.GiveRandomEquipment();
					PlayerCharacterMasterController.instances[i].master.inventory.GiveRandomItems(20, true, true);
				}
			};

			On.RoR2.CombatDirector.SetNextSpawnAsBoss += (orig, self) =>
			{
				enemyPlacement = DirectorPlacementRule.PlacementMode.Approximate;
				orig(self);
			};

			On.RoR2.TeleporterInteraction.AttemptSpawnPortal += (orig, self, portalSpawnCard, minDistance, maxDistance, successChatToken) =>
			{
				enemyPlacement = DirectorPlacementRule.PlacementMode.Random;
				return orig(self, portalSpawnCard, minDistance, maxDistance, successChatToken);
			};

			On.EntityStates.ScavMonster.FindItem.OnEnter += (orig, self) =>
			{
				// base.OnEnter() -> EntityStates.BaseState.OnEnter()
				if (self.characterBody)
				{
					self.attackSpeedStat = self.characterBody.attackSpeed;
					self.damageStat = self.characterBody.damage;
					self.critStat = self.characterBody.crit;
					self.moveSpeedStat = self.characterBody.moveSpeed;
				}

				Inventory component = self.GetComponent<Inventory>();
				float coeff = (float)Math.Pow(RoR2.Run.instance.time / 30, 2);
				if (coeff < 1)
					coeff = 1;

				Debug.LogFormat("Granting scavenger stack multiplier: {0}", new object[]
				{
					coeff
				});

				// ScavMonster.FindItem.OnEnter
				self.duration = EntityStates.ScavMonster.FindItem.baseDuration / self.attackSpeedStat;
				self.PlayCrossfade("Body", "SitRummage", "Sit.playbackRate", self.duration, 0.1f);
				Util.PlaySound(EntityStates.ScavMonster.FindItem.sound, base.gameObject);
				if (self.isAuthority)
				{
					WeightedSelection<List<PickupIndex>> weightedSelection = new WeightedSelection<List<PickupIndex>>(8);
					weightedSelection.AddChoice(Run.instance.availableTier1DropList.Where(new Func<PickupIndex, bool>(self.PickupIsNonBlacklistedItem)).ToList<PickupIndex>(), EntityStates.ScavMonster.FindItem.tier1Chance);
					weightedSelection.AddChoice(Run.instance.availableTier2DropList.Where(new Func<PickupIndex, bool>(self.PickupIsNonBlacklistedItem)).ToList<PickupIndex>(), EntityStates.ScavMonster.FindItem.tier2Chance);
					weightedSelection.AddChoice(Run.instance.availableTier3DropList.Where(new Func<PickupIndex, bool>(self.PickupIsNonBlacklistedItem)).ToList<PickupIndex>(), EntityStates.ScavMonster.FindItem.tier3Chance);
					List<PickupIndex> list = weightedSelection.Evaluate(UnityEngine.Random.value);
					self.dropPickup = list[UnityEngine.Random.Range(0, list.Count)];
					PickupDef pickupDef = PickupCatalog.GetPickupDef(self.dropPickup);
					if (pickupDef != null)
					{
						ItemDef itemDef = ItemCatalog.GetItemDef(pickupDef.itemIndex);
						if (itemDef != null)
						{
							self.itemsToGrant = 0;
							switch (itemDef.tier)
							{
								case ItemTier.Tier1:
									self.itemsToGrant = (int)(EntityStates.ScavMonster.FindItem.tier1Count * coeff);
									break;
								case ItemTier.Tier2:
									self.itemsToGrant = (int)(EntityStates.ScavMonster.FindItem.tier2Count * coeff);
									break;
								case ItemTier.Tier3:
									self.itemsToGrant = (int)(EntityStates.ScavMonster.FindItem.tier3Count * coeff);
									break;
								default:
									self.itemsToGrant = (int)(coeff);
									break;
							}
						}
					}
				}
				Transform transform = self.FindModelChild("PickupDisplay");
				self.pickupDisplay = transform.GetComponent<PickupDisplay>();
				self.pickupDisplay.SetPickupIndex(self.dropPickup, false);
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

			On.RoR2.SceneDirector.PopulateScene += (orig, self) =>
			{
				DirectorPlacementRule placementRule = new DirectorPlacementRule
				{
					placementMode = DirectorPlacementRule.PlacementMode.Random
				};
				InteractableSpawnCard spawnCard = Resources.Load<InteractableSpawnCard>("SpawnCards/InteractableSpawnCard/iscShrineRestack");
				for (int i = 0; i < 20; i++)
				{
					DirectorCore.instance.TrySpawnObject(new DirectorSpawnRequest(spawnCard, placementRule, self.rng));
				}

				orig(self);
			};

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
				self.lostBodyToDeath = false;
			}
			self.preventGameOver = true;
			self.killerBodyIndex = BodyIndex.None;
			self.killedByUnsafeArea = false;
			body.RecalculateStats();
			if (NetworkServer.active)
			{
				BaseAI[] array = self.aiComponents;
				for (int i = 0; i < array.Length; i++)
				{
					array[i].OnBodyStart(body);
				}
			}
			if (self.playerCharacterMasterController)
			{
				if (self.playerCharacterMasterController.networkUserObject)
				{
					bool isLocalPlayer = self.playerCharacterMasterController.networkUserObject.GetComponent<NetworkIdentity>().isLocalPlayer;
				}
				self.playerCharacterMasterController.OnBodyStart();
			}
			if (self.inventory.GetItemCount(RoR2Content.Items.Ghost) > 0)
			{
				Util.PlaySound("Play_item_proc_ghostOnKill", body.gameObject);
			}
			if (NetworkServer.active)
			{
				HealthComponent healthComponent = body.healthComponent;
				if (healthComponent)
				{
					if (self.teamIndex == TeamIndex.Player && Run.instance.selectedDifficulty >= DifficultyIndex.Eclipse1)
					{
						healthComponent.Networkhealth = healthComponent.fullHealth * 0.5f;
					}
					else if (self.teamIndex == TeamIndex.Monster)
					{
						//healthComponent.body.maxHealth *= 0.0001f;
						//healthComponent.body.maxShield = 0f;
						float coeff = RoR2.Run.instance.time / 10;
						Debug.LogFormat("Time Coefficient: {0}", new object[]
						{
							coeff
						});
						if (coeff < 1)
							coeff = 1;
						healthComponent.Networkhealth *= 0.001f * coeff;
						healthComponent.Networkshield = -1000f;
					}
					else
					{
						healthComponent.Networkhealth = healthComponent.fullHealth;
					}
				}
				self.UpdateBodyGodMode();
				self.StartLifeStopwatch();
			}
			self.SetUpGummyClone();
		}

		private bool AttemptSpawnOnTarget(RoR2.CombatDirector self, Transform spawnTarget, DirectorPlacementRule.PlacementMode placementMode = DirectorPlacementRule.PlacementMode.Approximate)
		{
			if (self.currentMonsterCard == null)
			{
				if (CombatDirector.cvDirectorCombatEnableInternalLogs.value)
				{
					Debug.Log("Current monster card is null, pick new one.");
				}
				if (self.finalMonsterCardsSelection == null)
				{
					return false;
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
			int num = self.currentMonsterCard.cost;
			int num2 = self.currentMonsterCard.cost;
			float num3 = 1f;
			EliteDef eliteDef = self.currentActiveEliteDef;
			num2 = (int)((float)num * self.currentActiveEliteTier.costMultiplier);
			if ((float)num2 <= self.monsterCredit)
			{
				num = num2;
				num3 = self.currentActiveEliteTier.costMultiplier;
			}
			else
			{
				self.ResetEliteType();
				eliteDef = self.currentActiveEliteDef;
			}
			if (!self.currentMonsterCard.IsAvailable())
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
			if (self.monsterCredit < (float)num)
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
			if (self.skipSpawnIfTooCheap && self.consecutiveCheapSkips < self.maxConsecutiveCheapSkips && (float)(num2 * self.maximumNumberToSpawnBeforeSkipping) < self.monsterCredit)
			{
				if (CombatDirector.cvDirectorCombatEnableInternalLogs.value)
				{
					Debug.LogFormat("Card {0} seems too cheap ({1}/{2}). Comparing against most expensive possible ({3})", new object[]
					{
				self.currentMonsterCard.spawnCard,
				num * self.maximumNumberToSpawnBeforeSkipping,
				self.monsterCredit,
				self.mostExpensiveMonsterCostInDeck
					});
				}
				if (self.mostExpensiveMonsterCostInDeck > num)
				{
					self.consecutiveCheapSkips++;
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
			spawnCard = Resources.Load<SpawnCard>("spawncards/characterspawncards/cscscav");
			SpawnCard spawnCard2 = spawnCard;
			EliteDef eliteDef2 = eliteDef;
			float valueMultiplier = num3;
			bool preventOverhead = self.currentMonsterCard.preventOverhead;
			if (self.Spawn(spawnCard2, eliteDef2, spawnTarget, self.currentMonsterCard.spawnDistance, preventOverhead, valueMultiplier, placementMode))
			{
				self.monsterCredit -= (float)num;
				self.totalCreditsSpent += (float)num;
				self.spawnCountInCurrentWave++;
				self.consecutiveCheapSkips = 0;
				return true;
			}
			return false;
		}
	}
}