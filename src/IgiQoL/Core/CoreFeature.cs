using System;
using System.Collections;
using Behaviour.UI;
using HarmonyLib;
using IgiQoL.Rules;
using Source.Player;
using Source.Util;

namespace IgiQoL.Core;

public sealed class CoreFeature : Feature
{
    public static CoreFeature Instance;

    public CoreFeature() { Instance = this; }

    public override string Id => "Core";
    public override string Description => "Core services (always on)";
    public override bool HasEnabledEntry => false;
    public override Type[] PatchClasses => new[] { typeof(SaveDataPatches), typeof(GameEventPatches) };

    public override void OnActivated()
    {
        GameEvents.SaveLoaded += ShipSettings.Invalidate;
        GameEvents.SaveLoaded += ItemRules.Load;
    }

    public override IEnumerator SelfCheck(SelfCheckContext ctx)
    {
        const string name = "save-roundtrip";
        const string saveName = "igiqol-selfcheck";
        string flagship = GamePlayer.current.currentSpaceShip.guid;
        string ticks = DateTime.UtcNow.Ticks.ToString();
        SaveData.Root["selfCheck"] = ticks;
        SaveData.Ship(flagship, true)["selfCheckMarker"] = true;
        ctx.Cleanup(() =>
        {
            SaveData.Root.Remove("selfCheck");
            SaveData.Ship(flagship, false)?.Remove("selfCheckMarker");
        });

        SaveGame.Store(SaveGame.SaveCurrentState(), saveName);
        SaveGame.InvalidateSaveCache();
        var file = SaveGame.GetSaveGame(saveName);
        if (file == null)
        {
            ctx.Fail(name, "save file not found after Store");
            yield break;
        }
        ctx.Cleanup(() => { file.Delete(); SaveGame.InvalidateSaveCache(); });

        var json = file.Recall();
        var igi = json["Player"]["igiQol"];
        if (!igi.IsJsonObject)
        {
            ctx.Fail(name, "Player.igiQol missing from the written save");
            yield break;
        }
        string foundTicks = igi["selfCheck"].AsString;
        var ships = igi["ships"];
        var marker = ships.IsJsonObject ? ships[flagship] : default;
        bool markerOk = marker.IsJsonObject && marker["selfCheckMarker"].IsBoolean && marker["selfCheckMarker"].AsBoolean;
        if (foundTicks == ticks && markerOk)
            ctx.Pass(name, $"igiQol.selfCheck={foundTicks}, ships[{flagship}].selfCheckMarker=true");
        else
            ctx.Fail(name, $"selfCheck={foundTicks ?? "null"} (expected {ticks}), marker={(marker.IsNull ? "missing" : marker.ToString())}");
    }
}
