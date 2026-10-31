using System.Text;
using Behaviour.UI;

namespace IgiQoL.Core;

public static class StartupReport
{
    private static bool shown;

    public static void ShowOnce()
    {
        if (shown || FeatureRegistry.Failures.Count == 0) return;
        var sb = new StringBuilder();
        sb.Append("Igi's QoL Modpack switched off ").Append(FeatureRegistry.Failures.Count).Append(" feature(s) after a game update:\n");
        foreach (var f in FeatureRegistry.Failures)
            sb.Append("- ").Append(f.Id).Append(": ").Append(f.Error.GetType().Name).Append(" at ").Append(f.Where).Append('\n');
        sb.Append("Details: BepInEx/LogOutput.log");
        if (AlertPopup.ShowMessage(sb.ToString()) != null) shown = true;
    }
}
