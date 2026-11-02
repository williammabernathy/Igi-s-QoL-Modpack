using System.Text;
using Behaviour.UI;

namespace IgiQoL.Core;

public static class StartupReport
{
    private static int reported;

    public static void ShowOnce()
    {
        if (FeatureRegistry.Failures.Count <= reported) return;
        var sb = new StringBuilder();
        sb.Append("Igi's QoL Modpack switched off ").Append(FeatureRegistry.Failures.Count).Append(" feature(s):\n");
        foreach (var f in FeatureRegistry.Failures)
            sb.Append("- ").Append(f.Id).Append(": ").Append(f.Error.GetType().Name).Append(" at ").Append(f.Where).Append('\n');
        sb.Append("Details: BepInEx/LogOutput.log");
        if (AlertPopup.ShowMessage(sb.ToString()) != null) reported = FeatureRegistry.Failures.Count;
    }
}
