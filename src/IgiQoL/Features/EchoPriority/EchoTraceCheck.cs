using IgiQoL.Core;

namespace IgiQoL.Features.EchoPriority;

// The loop detector on fabricated trip sequences. RecordHop only returns the report line, so nothing reaches the logs.
internal static class EchoTraceCheck
{
    private static string Hop(string from, string to, float at, string status = "Going somewhere", string step = "self-check")
        => EchoTrace.RecordHop(from, to, at, status, "'Mine Iron' at Field, accepted by you", step, null, "Mining Surface");

    public static void Run(SelfCheckContext ctx)
    {
        try
        {
            EchoTrace.Clear();
            string r1 = Hop("Tower I", "Field", 0f);
            string r2 = Hop("Field", "Tower I", 60f);
            string r3 = Hop("Tower I", "Field", 120f);
            string r4 = Hop("Field", "Tower I", 180f, "Returning to the home base", "activity list, entry 1 (Combat)");
            string r5 = Hop("Tower I", "Field", 240f);
            string d = $"after 3 trips: {(r3 == null ? "no report" : "report")}; after 4: {(r4 == null ? "no report" : "report")}; fifth trip of the same pair: {(r5 == null ? "no report" : "report")}";
            bool text = r4 != null && r4.Contains("Tower I") && r4.Contains("Field") && r4.Contains("Returning to the home base")
                && r4.Contains("activity list, entry 1 (Combat)") && r4.Contains("accepted by you") && r4.Contains("Mining Surface");
            if (r1 == null && r2 == null && r3 == null && r4 != null && r5 == null && text) ctx.Pass("loop-detected", d);
            else ctx.Fail("loop-detected", d + $"; names both places, status, decision and mission: {text}; line: {r4}");

            EchoTrace.Clear();
            string a = Hop("Tower I", "Field", 0f), b = Hop("Field", "Belt", 60f), c = Hop("Belt", "Tower I", 120f),
                e = Hop("Tower I", "Field", 180f), g = Hop("Field", "Belt", 240f), h = Hop("Belt", "Tower I", 300f);
            if (a == null && b == null && c == null && e == null && g == null && h == null) ctx.Pass("loop-not-three-places", "A, B, C, A, B, C: no report");
            else ctx.Fail("loop-not-three-places", "A, B, C, A, B, C reported: " + (b ?? c ?? e ?? g ?? h));

            EchoTrace.Clear();
            string s1 = Hop("Tower I", "Field", 0f), s2 = Hop("Field", "Tower I", 400f), s3 = Hop("Tower I", "Field", 800f), s4 = Hop("Field", "Tower I", 1200f);
            if (s1 == null && s2 == null && s3 == null && s4 == null) ctx.Pass("loop-not-slow", "A, B, A, B six or more minutes apart: no report");
            else ctx.Fail("loop-not-slow", "slow trips reported: " + s4);

            // The same pair again after ECHO went somewhere else is a new loop.
            EchoTrace.Clear();
            Hop("Tower I", "Field", 0f); Hop("Field", "Tower I", 30f); Hop("Tower I", "Field", 60f);
            string first = Hop("Field", "Tower I", 90f);
            Hop("Tower I", "Belt", 120f);
            Hop("Belt", "Tower I", 150f); Hop("Tower I", "Field", 180f); Hop("Field", "Tower I", 210f);
            string again = Hop("Tower I", "Field", 240f);
            if (first != null && again != null) ctx.Pass("loop-reported-again", "second loop of the same pair after a trip elsewhere reported again");
            else ctx.Fail("loop-reported-again", $"first {(first != null)}, again {(again != null)}");
        }
        finally { EchoTrace.Clear(); }
    }
}
