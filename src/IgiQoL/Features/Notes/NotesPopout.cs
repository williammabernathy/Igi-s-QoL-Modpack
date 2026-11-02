using System;
using System.Collections.Generic;
using Behaviour.UI.Shared;
using Behaviour.UI.Spacestation;
using IgiQoL.UI;
using Source.Util;
using TMPro;
using UnityEngine;

namespace IgiQoL.Features.Notes;

// One note per window, read-only, as a checklist or reference beside the game. Editing stays on the tab, so there's
// one editor and nothing to overwrite; the window redraws when the note changes. A note keeps its window (and where
// you left it) after you dock it back; deleting the note deletes its windows.
internal static class NotesPopout
{
    public const string KindId = "note";
    internal const string NoteOption = "note";

    private static readonly List<string> scratch = new();

    internal static void Register(Notes owner) => Popouts.Register(new PopoutKind
    {
        Id = KindId,
        Title = "Note",
        Owner = owner,
        Multiple = true,
        DockTab = "notes",
        DefaultSize = new Vector2(380f, 320f),
        MinSize = new Vector2(220f, 100f),
        RelevantLabel = "you're docked",
        Relevant = () => SpaceStationInterior.instance != null,
        TitleOf = s => Find(s)?.name ?? "Note",
        Build = (p, body) => body.gameObject.AddComponent<NoteView>().Init(p, body),
        BuildOptions = BuildOptions,
    });

    internal static NoteTab Find(PopoutState s)
    {
        var v = s.options[NoteOption];
        if (!v.IsString) return null;
        foreach (var t in NotesStore.Tabs)
            if (t.id == v.AsString) return t;
        return null;
    }

    private static bool Shows(string id, NoteTab note)
    {
        var v = Popouts.State(id).options[NoteOption];
        return v.IsString && v.AsString == note.id;
    }

    internal static bool IsOut(NoteTab note)
    {
        foreach (var id in Popouts.Windows(KindId))
            if (Popouts.IsOpen(id) && Shows(id, note)) return true;
        return false;
    }

    // Opens the note's window where you left it last time, or a new one.
    internal static string PopOut(NoteTab note)
    {
        foreach (var id in Popouts.Windows(KindId))
            if (Shows(id, note))
            {
                Popouts.Open(id);
                return id;
            }
        return Popouts.OpenNew(KindId, s => s.options[NoteOption] = note.id);
    }

    internal static void DockBack(NoteTab note)
    {
        foreach (var id in Windows(note))
            if (Popouts.IsOpen(id)) Popouts.Close(id);
    }

    // A deleted note takes its windows with it.
    internal static void Forget(NoteTab note)
    {
        foreach (var id in Windows(note)) Popouts.Remove(id);
    }

    // A renamed note's windows get the new title.
    internal static void Retitle(NoteTab note)
    {
        foreach (var id in Windows(note)) Popouts.Rebuild(id);
    }

    // A copy, so callers can remove windows while they go through it.
    private static List<string> Windows(NoteTab note)
    {
        scratch.Clear();
        foreach (var id in Popouts.Windows(KindId))
            if (Shows(id, note)) scratch.Add(id);
        return scratch;
    }

    private static void BuildOptions(Popout p, RectTransform column)
    {
        var row = Kit.Row(column, 8f, 26f);
        Kit.Label(row, "Note");
        var shown = Find(p.State);
        var names = new List<string>();
        foreach (var t in NotesStore.Tabs) names.Add(t.name);
        Kit.Dropdown(row, names, Mathf.Max(0, NotesStore.Tabs.IndexOf(shown)), i =>
        {
            p.State.options[NoteOption] = NotesStore.Tabs[i].id;
            p.State.Save(p.Id);
            Popouts.Rebuild(p.Id);
        }, 200f);
    }
}

internal sealed class NoteView : MonoBehaviour
{
    private Popout popout;
    private int version = -1;
    internal TMP_Text label;

    internal void Init(Popout p, RectTransform body)
    {
        popout = p;
        var inner = Kit.Scroll(body);
        label = UiCards.AddLabel(inner, "", 13f, ColorHelper.offWhite);
        label.raycastTarget = true;
        label.richText = true;
        label.gameObject.AddComponent<LinkClicks>();
        Redraw();
    }

    private void Update()
    {
        if (!Notes.Instance.Active || version == NotesStore.Version) return;
        try { Redraw(); }
        catch (Exception e) { Notes.Instance.Fail(e, nameof(NoteView)); }
    }

    private void Redraw()
    {
        version = NotesStore.Version;
        var note = NotesPopout.Find(popout.State);
        label.text = note == null ? "This note was deleted. Pick another one under Options."
            : note.text.Length == 0 ? "This note is empty. Write in it on the Notes tab."
            : NotesMarkdown.Render(note.text);
    }
}
