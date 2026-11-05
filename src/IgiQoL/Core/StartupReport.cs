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
        sb.Append("Igi's QoL Modpack switched off ").Append(FeatureRegistry.Failures.Count == 1 ? "a feature" : FeatureRegistry.Failures.Count + " features")
            .Append(" because of an error. The rest of the modpack keeps working.\n");
        foreach (var f in FeatureRegistry.Failures)
        {
            var feature = System.Array.Find(FeatureRegistry.All, x => x.Id == f.Id);
            sb.Append("- ").Append(feature != null ? feature.Title : f.Id).Append(" (").Append(f.Error.GetType().Name).Append(" in ").Append(f.Where).Append(")\n");
        }
        sb.Append("The full error is in BepInEx/LogOutput.log.");
        if (AlertPopup.ShowMessage(sb.ToString()) != null) reported = FeatureRegistry.Failures.Count;
    }
}
