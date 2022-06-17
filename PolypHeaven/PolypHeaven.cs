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

namespace PolypHeaven
{

    [BepInDependency(R2API.R2API.PluginGUID)]

    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
	
    [R2APISubmoduleDependency(nameof(ItemAPI), nameof(LanguageAPI))]
	
    public class PolypHeaven : BaseUnityPlugin
	{
        public const string PluginGUID = PluginAuthor + "." + PluginName;
        public const string PluginAuthor = "roytu";
        public const string PluginName = "PolypHeaven";
        public const string PluginVersion = "0.0.1";


		public void Awake()
		{
            On.RoR2.PickupDropTable.GenerateDrop += (orig, self, rng) =>
            {
                return new RoR2.PickupIndex(169);
            };

			On.RoR2.PickupDropTable.GenerateDropFromWeightedSelection += (orig, rng, weightedSelection) =>
			{
				return new RoR2.PickupIndex(169);
			};

			On.RoR2.SceneDirector.PopulateScene += (orig, self) =>
			{
				DirectorPlacementRule placementRule = new DirectorPlacementRule
				{
					placementMode = DirectorPlacementRule.PlacementMode.Random
				};
				InteractableSpawnCard spawnCard = Resources.Load<InteractableSpawnCard>("SpawnCards/InteractableSpawnCard/iscShrineRestack");
				for (int i = 0; i < 500; i++)
				{
					DirectorCore.instance.TrySpawnObject(new DirectorSpawnRequest(spawnCard, placementRule, self.rng));
				}

				orig(self);
			};
		}
	}
}