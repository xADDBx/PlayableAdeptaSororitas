using HarmonyLib;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.JsonSystem;
using Kingmaker.Blueprints.Root;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.UI.MVVM.VM.CharGen;
using Kingmaker.UI.MVVM.VM.CharGen.Phases.Appearance;
using Kingmaker.UI.MVVM.VM.CharGen.Phases.Appearance.Pages;
using Kingmaker.UnitLogic.Progression.Paths;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityModManagerNet;
using System.IO;
using Kingmaker.UnitLogic.Levelup.Selections;
using Kingmaker.UnitLogic.Progression.Features;
using Kingmaker.UnitLogic.Levelup.Obsolete;
using Kingmaker.UnitLogic.Progression.Prerequisites;
using Kingmaker.UnitLogic.Levelup;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.FactLogic;
using Kingmaker.EntitySystem.Entities.Base;
using System.Collections.Generic;
using Kingmaker.UnitLogic.Parts;
using Kingmaker.ResourceLinks;
using Kingmaker.UnitLogic.Levelup.Selections.Doll;
using Kingmaker.Visual.CharacterSystem;
using Kingmaker.Mechanics.Entities;
using Kingmaker.PubSubSystem.Core;
using Kingmaker.Controllers.StarSystem;
using Owlcat.Runtime.Core;
using Kingmaker.View;
using Kingmaker.Blueprints.Area;
using Kingmaker.EntitySystem.Persistence;
using Kingmaker;

namespace PlayableAdeptaSororitas;

#if DEBUG
[EnableReloading]
#endif
public static class Main {
    internal static Harmony HarmonyInstance;
    internal static UnityModManager.ModEntry.ModLogger log;
    public static bool Load(UnityModManager.ModEntry modEntry) {
        log = modEntry.Logger;
#if DEBUG
        modEntry.OnUnload = OnUnload;
#endif
        modEntry.OnGUI = OnGUI;
        HarmonyInstance = new Harmony(modEntry.Info.Id);
        HarmonyInstance.PatchAll(Assembly.GetExecutingAssembly());
        return true;
    }
    internal static bool createAdeptaSororitas = false;
    public static void OnGUI(UnityModManager.ModEntry modEntry) {
        GUILayout.Label("Should the next character creation be a Adepta Sororitas?");
        createAdeptaSororitas = GUILayout.Toggle(createAdeptaSororitas, "Create Adepta Sororitas");
    }

#if DEBUG
    public static bool OnUnload(UnityModManager.ModEntry modEntry) {
        HarmonyInstance.UnpatchAll(modEntry.Info.Id);
        return true;
    }
#endif    
    [HarmonyPatch(typeof(CharGenConfig), nameof(CharGenConfig.Create))]
    internal static class CharGenConfig_Create_Patch {
        [HarmonyPrefix]
        private static void Create(CharGenConfig.CharGenMode mode) {
            if (createAdeptaSororitas) {
                CharGenContext_GetOriginPath_Patch.isMercenary = mode == CharGenConfig.CharGenMode.NewCompanion;
            }
        }
    }
    [HarmonyPatch(typeof(CharGenContext), nameof(CharGenContext.GetOriginPath))]
    internal static class CharGenContext_GetOriginPath_Patch {
        internal static bool isMercenary = false;
        [HarmonyPostfix]
        private static void GetOriginPath(ref BlueprintOriginPath __result) {
            if (createAdeptaSororitas) {
                var copy = CopyBlueprint(__result);
                try {
                    var c = copy.Components.OfType<AddFeaturesToLevelUp>().Where(c => c.Group == FeatureGroup.ChargenOccupation).First();
                    var adeptaSororitasOccupation = ResourcesLibrary.BlueprintsCache.Load("b6962fcc54054af98961dd9a6c0f9e18") as BlueprintFeature;
                    c.m_Features = new[] { adeptaSororitasOccupation.ToReference<BlueprintFeatureReference>() }.AddRangeToArray(c.m_Features);
                    copy.Components[1] = c;
                    __result = copy;
                } catch (Exception e) {
                    log.Log(e.ToString());
                }
            }
        }
        private static T CopyBlueprint<T>(T bp) where T : SimpleBlueprint {
            var writer = new StringWriter();
            var serializer = JsonSerializer.Create(Json.Settings);
            serializer.Serialize(writer, new BlueprintJsonWrapper(bp));
            return serializer.Deserialize<BlueprintJsonWrapper>(new JsonTextReader(new StringReader(writer.ToString()))).Data as T;
        }
    }
#pragma warning disable CS0612 // Type or member is obsolete
    [HarmonyPatch(typeof(Prerequisite), nameof(Prerequisite.Meet))]
#pragma warning restore CS0612 // Type or member is obsolete
    internal static class Prerequisite_Meet_Patch {
        [HarmonyPostfix]
        private static void Meet(ref bool __result, Prerequisite __instance, IBaseUnitEntity unit) {
            var adeptaSororitasOccupation = ResourcesLibrary.BlueprintsCache.Load("b6962fcc54054af98961dd9a6c0f9e18") as BlueprintFeature;
            Feature feature = unit.ToBaseUnitEntity().Facts.Get(adeptaSororitasOccupation) as Feature;
            if (createAdeptaSororitas && feature != null) {
                var key = __instance.Owner.name;
                if ((key?.Contains("DarkestHour") ?? false) || (key?.Contains("MomentOfTriumph") ?? false)) {
                    __result = true;
                }
            }
        }
    }
    private static List<string> EEIds = new() { "8c2fbd6dc40d20f4595b0d6723c12156", "b10cda35b4ee46047a53bc744e0a12b4", "b969c7a1210d135458fe83a1c348c615", "59e3485bef4a2d6438cb273ec4b82e79", "19c1588eb86661142a277baf91b0fee0" };
    [HarmonyPatch(typeof(CharGenContextVM), nameof(CharGenContextVM.CompleteCharGen))]
    internal static class CharGenContextVM_ComplteCharGen_Patch {
        [HarmonyPrefix]
        private static void CompleteCharGen(BaseUnitEntity resultUnit) {
            var adeptaSororitasOccupation = ResourcesLibrary.BlueprintsCache.Load("b6962fcc54054af98961dd9a6c0f9e18") as BlueprintFeature;
            Feature feature = resultUnit.Facts.Get(adeptaSororitasOccupation) as Feature;
            if (createAdeptaSororitas && feature != null) {
                EntityPartStorage.perSave.AddClothes[resultUnit.UniqueId] = EEIds; 
                EntityPartStorage.SavePerSaveSettings();
            }
        }
    }
    [HarmonyPatch(typeof(PartUnitProgression))]
    internal static class PartUnitProgression_Patch {
        [HarmonyPatch(nameof(PartUnitProgression.AddFeatureSelection))]
        [HarmonyPrefix]
        private static void AddFeatureSelection(ref BlueprintPath path) {
            if (createAdeptaSororitas && path is BlueprintOriginPath) {
                path = CharGenContext_GetOriginPath_Patch.isMercenary ? BlueprintCharGenRoot.Instance.NewCompanionCustomChargenPath : BlueprintCharGenRoot.Instance.NewGameCustomChargenPath;
            }
        }
        [HarmonyPatch(nameof(PartUnitProgression.AddPathRank))]
        [HarmonyPrefix]
        private static void AddPathRank(ref BlueprintPath path) {
            if (createAdeptaSororitas && path is BlueprintOriginPath) {
                path = CharGenContext_GetOriginPath_Patch.isMercenary ? BlueprintCharGenRoot.Instance.NewCompanionCustomChargenPath : BlueprintCharGenRoot.Instance.NewGameCustomChargenPath;
            }
        }
    }
    [HarmonyPatch(typeof(PartUnitViewSettings), nameof(PartUnitViewSettings.Instantiate))]
    internal static class PartUnitViewSettings_Instantiate_Patch {
        [HarmonyPrefix]
        private static void Instant_Pre(PartUnitViewSettings __instance) {
            DollData_CreateUnitView_Patch.context = __instance.Owner;
        }
        [HarmonyPostfix]
        private static void Instant_Post() {
            DollData_CreateUnitView_Patch.context = null;
        }
    }
    [HarmonyPatch(typeof(DollState), nameof(DollState.CollectMechanicEntities))]
    internal static class DollState_CollectMechanicEntities_Patch {
        [HarmonyPostfix]
        private static void CollectMechanicEntitities(DollState __instance, ref IEnumerable<EquipmentEntityLink> __result, BaseUnitEntity unit) {
            var adeptaSororitasOccupation = ResourcesLibrary.BlueprintsCache.Load("b6962fcc54054af98961dd9a6c0f9e18") as BlueprintFeature;
            Feature feature = unit.Facts.Get(adeptaSororitasOccupation) as Feature;
            if (createAdeptaSororitas && feature != null) {
                var eels = EEIds.Select(id => new EquipmentEntityLink() { AssetId = id });
                var res = __result.ToList();
                res.AddRange(eels);
                __result = res.AsEnumerable();
            }
        }
    }
    [HarmonyPatch(typeof(DollData), nameof(DollData.CreateUnitView))]
    internal static class DollData_CreateUnitView_Patch {
        internal static AbstractUnitEntity context = null;
        [HarmonyPostfix]
        private static void CreateUnitView(DollData __instance, ref UnitEntityView __result, bool savedEquipment) {
            if (EntityPartStorage.perSave.AddClothes.TryGetValue(context.UniqueId, out var ees)) {
                Character component2 = __result.GetComponent<Character>();
                foreach (var eeId in ees) {
                    var eel = new EquipmentEntityLink() { AssetId = eeId };
                    var ee = eel.Load();
                    if (!component2.EquipmentEntities.Where(e => e.name == ee.name).Any()) {
                        component2.AddEquipmentEntity(ee, savedEquipment);
                    }
                }
                __instance.ApplyRampIndices(component2, savedEquipment);
            }
        }
    }
    [HarmonyPatch(typeof(Game))]
    internal static class Game_Patch {
        [HarmonyPatch(nameof(Game.LoadArea), new Type[] { typeof(BlueprintArea), typeof(BlueprintAreaEnterPoint), typeof(AutoSaveMode), typeof(SaveInfo), typeof(Action) })]
        [HarmonyPrefix]
        private static void LoadArea() {
            EntityPartStorage.ClearCachedPerSave();
        }
    }
}