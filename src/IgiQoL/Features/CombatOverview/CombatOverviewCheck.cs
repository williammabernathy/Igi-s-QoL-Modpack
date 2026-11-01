using System.Collections;
using Behaviour.Unit;
using IgiQoL.Core;
using IgiQoL.Game;
using IgiQoL.UI;
using Source.Combat;
using UnityEngine;
using Object = UnityEngine.Object;
using Stats = IgiQoL.Features.ShipStats.ShipStats;

namespace IgiQoL.Features.CombatOverview;

internal static class CombatOverviewCheck
{
    internal static IEnumerator Run(SelfCheckContext ctx)
    {
        const string id = CombatOverview.PopoutId;
        string pref = PlayerPrefs.GetString("IgiQoL.Popout." + id, "");
        bool wasOpen = Popouts.IsOpen(id);
        ctx.Cleanup(() =>
        {
            Popouts.Close(id);
            PlayerPrefs.SetString("IgiQoL.Popout." + id, pref);
            Popouts.ForgetState(id);
            if (wasOpen) Popouts.Open(id);
        });

        yield return SelfCheckContext.EnsureInSpace();
        Popouts.Close(id);
        var state = Popouts.State(id);
        state.show = PopoutShow.Always;
        state.collapsed = false;
        state.clickThrough = true;
        state.opacity = 80;
        state.options[CombatOverview.ShipsOption] = ShipsShown.All.ToString();
        // A layout saved far off screen and larger than the screen comes back inside it.
        state.placed = true;
        state.x = 99999f;
        state.y = -99999f;
        state.w = 99999f;
        state.h = 400f;
        Popouts.Open(id);
        var p = Popouts.Get(id);
        if (p == null)
        {
            ctx.Fail("open", "the pop-out did not open");
            yield break;
        }
        var rt = (RectTransform)p.transform;
        var canvas = ((RectTransform)rt.parent).rect.size;
        var reloaded = PopoutState.Load(Popouts.Find(id));
        string layoutDetail = $"pos=({rt.anchoredPosition.x:0},{rt.anchoredPosition.y:0}) size=({rt.sizeDelta.x:0},{rt.sizeDelta.y:0}) canvas=({canvas.x:0},{canvas.y:0}) savedOpen={reloaded.open}";
        bool inside = rt.anchoredPosition.x >= -canvas.x / 2f - 0.5f && rt.anchoredPosition.x + rt.sizeDelta.x <= canvas.x / 2f + 0.5f
                      && rt.anchoredPosition.y <= canvas.y / 2f + 0.5f && rt.anchoredPosition.y - rt.sizeDelta.y >= -canvas.y / 2f - 0.5f;
        if (inside && rt.sizeDelta.x <= canvas.x + 0.5f && reloaded.open) ctx.Pass("clamp-on-load", layoutDetail);
        else ctx.Fail("clamp-on-load", layoutDetail);

        // Collapse keeps the header only; expanding brings the saved height back.
        var collapse = FindButton(p, "-");
        collapse?.onClick.Invoke();
        float collapsedHeight = rt.sizeDelta.y;
        bool bodyHidden = !p.Body.gameObject.activeSelf;
        collapse?.onClick.Invoke();
        string collapseDetail = $"collapsed height={collapsedHeight:0} bodyHidden={bodyHidden} expanded height={rt.sizeDelta.y:0} saved h={state.h:0}";
        if (collapse != null && Mathf.Approximately(collapsedHeight, Popout.HeaderHeight) && bodyHidden && Mathf.Approximately(rt.sizeDelta.y, state.h)) ctx.Pass("collapse", collapseDetail);
        else ctx.Fail("collapse", collapseDetail);

        // Put it somewhere sensible for the rest of the check and the screenshot.
        rt.anchoredPosition = new Vector2(-canvas.x / 2f + 20f, canvas.y / 2f - 140f);
        rt.sizeDelta = new Vector2(state.w = 760f, state.h = 400f);
        p.Persist();

        // Click-through: the body lets clicks pass, the header still takes them; off, the body takes them again.
        yield return null;
        bool passes = p.ClicksPass;
        var header = p.transform.Find("Frame/Header");
        var headerGroupBlocks = header != null && header.GetComponentInParent<CanvasGroup>().blocksRaycasts;
        state.clickThrough = false;
        yield return null;
        bool blocksAfter = !p.ClicksPass;
        state.clickThrough = true;
        string clickDetail = $"bodyPasses={passes} headerBlocks={headerGroupBlocks} offBlocks={blocksAfter} alpha={p.GetComponent<CanvasGroup>().alpha:0.00}";
        if (passes && headerGroupBlocks && blocksAfter && Mathf.Approximately(p.GetComponent<CanvasGroup>().alpha, 0.8f)) ctx.Pass("click-through", clickDetail);
        else ctx.Fail("click-through", clickDetail);

        // Enemies for the list; earlier checks' spawns may be dead by now.
        int enemies = CountEnemies();
        if (enemies < 2)
        {
            SelfCheckContext.SpawnEnemies(2);
            float until = Time.realtimeSinceStartup + 20f;
            while (CountEnemies() < 2 && Time.realtimeSinceStartup < until) yield return new WaitForSeconds(1f);
        }
        yield return new WaitForSeconds(1.2f);
        var list = OverviewList.Instance;
        // Counted right after a refresh, so ships arriving or dying in between don't skew it.
        list?.Tick();
        int ships = 0;
        SpaceShip enemy = null;
        foreach (var s in Object.FindObjectsByType<SpaceShip>(FindObjectsInactive.Exclude))
        {
            if (s == null || s.isDestroyed || s.isCarrierFighter || !s.gameObject.activeInHierarchy) continue;
            ships++;
            if (enemy == null && s.IsPlayerEnemy()) enemy = s;
        }
        string rowsDetail = $"rows={(list != null ? list.RowCount : -1)} ships={ships} enemy={(enemy != null ? Aggro.Name(enemy) : "none")}";
        if (list != null && list.RowCount == ships && ships > 0) ctx.Pass("rows", rowsDetail);
        else ctx.Fail("rows", rowsDetail);

        if (list == null || enemy == null) ctx.Skip("details", "no enemy in the list");
        else
        {
            list.ToggleDetails(enemy);
            string details = list.Details(enemy) ?? "";
            string want = Stats.Pct(Resists.Multiplier(enemy, DamageType.Kinetic));
            // A weak type's figure is coloured red, so compare the text without the colour tags.
            string plain = System.Text.RegularExpressions.Regex.Replace(details, "<[^>]+>", "");
            bool resists = plain.Contains(DamageType.Kinetic.GetDisplayName() + " " + want);
            string detail = $"{Aggro.Name(enemy)}: kinetic {want} shown={resists}; {plain.Replace("\n", " / ")}";
            if (resists && plain.Contains("Weapons") && plain.Contains("Attacked by")) ctx.Pass("details", detail);
            else ctx.Fail("details", detail);
        }

        // "Only in combat" follows the same test the window uses; "Always" shows it whatever happens.
        state.show = PopoutShow.WhenRelevant;
        yield return new WaitForSecondsRealtime(0.4f);
        bool combat = CombatOverview.InCombat();
        bool shownInCombatMode = p.Shown;
        state.show = PopoutShow.Always;
        yield return new WaitForSecondsRealtime(0.4f);
        string showDetail = $"inCombat={combat} shownWhenRelevant={shownInCombatMode} shownAlways={p.Shown}";
        if (shownInCombatMode == combat && p.Shown) ctx.Pass("show-condition", showDetail);
        else ctx.Fail("show-condition", showDetail);

        yield return ctx.Screenshot("combat-overview");

        // The mod window lists the open pop-out in its bottom strip.
        ModWindow.Open("combat");
        yield return null;
        var strip = ModWindow.IsOpen ? GameObject.Find("IgiQoL_Window/PopoutStrip") : null;
        string stripDetail = $"window={ModWindow.IsOpen} strip={(strip != null && strip.activeSelf)} children={(strip != null ? strip.transform.childCount : 0)}";
        if (strip != null && strip.activeSelf && strip.transform.childCount >= 3) ctx.Pass("strip", stripDetail);
        else ctx.Fail("strip", stripDetail);
        yield return ctx.Screenshot("combat-overview-strip");
        ModWindow.Close();
    }

    private static int CountEnemies()
    {
        int n = 0;
        foreach (var s in Object.FindObjectsByType<SpaceShip>(FindObjectsInactive.Exclude))
            if (s != null && !s.isDestroyed && s.IsPlayerEnemy()) n++;
        return n;
    }

    private static UnityEngine.UI.Button FindButton(Popout p, string caption)
    {
        foreach (var b in p.GetComponentsInChildren<UnityEngine.UI.Button>())
        {
            var t = b.GetComponentInChildren<TMPro.TMP_Text>();
            if (t != null && t.text == caption) return b;
        }
        return null;
    }
}
