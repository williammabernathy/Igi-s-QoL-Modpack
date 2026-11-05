using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Behaviour.UI;
using Behaviour.UI.Shared;
using Behaviour.UI.Side_Menu.SideTabs.Captain;
using BepInEx;
using IgiQoL.Core;
using IgiQoL.UI;
using LightJson;
using Source.Util;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IgiQoL.Features.Notes;

public sealed class Notes : Feature
{
    public static Notes Instance;

    public Notes() { Instance = this; }

    public override string Id => "Notes";
    public override string Description => "The Notes tab: your own notes and links, kept in BepInEx/config/IgiQoL/notes.json rather than in a save. Any note can also float in a window of its own, read-only.";
    public override Type[] PatchClasses => Type.EmptyTypes;

    public override void OnActivated()
    {
        NotesStore.Load();
        ModWindow.AddTab(this, "notes", "Notes", 30, NotesTab.Build);
        NotesPopout.Register(this);
    }

    public override IEnumerator SelfCheck(SelfCheckContext ctx)
    {
        const string source = "# T\n**b** [x](https://a.b) https://c.d <q>";
        string rendered = NotesMarkdown.Render(source);
        string[] needles =
        {
            "<size=140%><b>T</b></size>", "<b>b</b>", "<link=\"https://a.b\">", "<link=\"https://c.d\">", "<noparse><</noparse>",
        };
        string missing = null;
        foreach (var n in needles)
            if (!rendered.Contains(n)) { missing = n; break; }
        if (missing == null) ctx.Pass("render", rendered.Replace("\n", "\\n"));
        else ctx.Fail("render", $"missing {missing} in {rendered.Replace("\n", "\\n")}");

        string text = "roundtrip " + DateTime.UtcNow.Ticks;
        NotesStore.Tabs.RemoveAll(t => t.name == "selfcheck");   // left behind if an earlier run was killed before its cleanup
        var added = new NoteTab { name = "selfcheck", text = text };
        NotesStore.Tabs.Add(added);
        ctx.Cleanup(() =>
        {
            NotesStore.Tabs.RemoveAll(t => t.name == "selfcheck");
            NotesStore.Save();
        });
        NotesStore.Save();
        NotesStore.Load();
        NoteTab found = NotesStore.Tabs.Find(t => t.name == "selfcheck");
        if (found != null && found.text == text && found.id == added.id) ctx.Pass("file-roundtrip", $"tabs={NotesStore.Tabs.Count} path={NotesStore.FilePath}");
        else ctx.Fail("file-roundtrip", found == null ? "tab missing after reload" : $"text '{found.text}' != '{text}' or id '{found.id}' != '{added.id}'");

        ModWindow.Open("notes");
        ctx.Cleanup(ModWindow.Close);
        var tab = ModWindow.IsOpen && ModWindow.Content != null ? ModWindow.Content.GetComponent<NotesTab>() : null;
        if (tab != null && (tab.editor != null || tab.viewer != null))
            ctx.Pass("tab", tab.editor != null ? "editor present" : "viewer present");
        else ctx.Fail("tab", $"open={ModWindow.IsOpen} tab={(tab != null)}");
        yield return ctx.Screenshot("window-notes");

        if (found != null) yield return CheckPopout(ctx, found);
    }

    // A note's window shows it read-only, follows edits, docks back into the tab, comes back in the same window, and
    // goes when the note is deleted.
    private static IEnumerator CheckPopout(SelfCheckContext ctx, NoteTab note)
    {
        string listPref = PopoutState.Pref(NotesPopout.KindId) + ".list";
        string savedList = PlayerPrefs.GetString(listPref, "");
        var created = new List<string>();
        ctx.Cleanup(() =>
        {
            foreach (var id in created) Popouts.Remove(id);
            PlayerPrefs.SetString(listPref, savedList);
            Popouts.ForgetState(NotesPopout.KindId);
        });

        string first = NotesPopout.PopOut(note);
        if (first != null) created.Add(first);
        var p = first != null ? Popouts.Get(first) : null;
        if (p == null)
        {
            ctx.Fail("popout", "the pop-out did not open");
            yield break;
        }
        p.State.show = PopoutShow.Always;
        yield return null;
        var view = p.GetComponentInChildren<NoteView>();
        string title = null;
        foreach (var t in p.transform.Find("Frame/Header").GetComponentsInChildren<TMP_Text>())
            if (t.text == note.name) title = t.text;
        string d = $"id={first} title={title} text='{(view != null ? view.label.text : null)}' out={NotesPopout.IsOut(note)}";
        if (view != null && title != null && view.label.text == NotesMarkdown.Render(note.text) && NotesPopout.IsOut(note)) ctx.Pass("popout", d);
        else ctx.Fail("popout", d);

        note.text = "# Checklist\n- one\n- two";
        NotesStore.Save();
        yield return null;
        d = $"text='{(view != null ? view.label.text : null)}'";
        if (view != null && view.label.text == NotesMarkdown.Render(note.text)) ctx.Pass("popout-follows", d);
        else ctx.Fail("popout-follows", d);
        // Pop-outs draw under the mod window, so close it for the picture and bring the Notes tab back after.
        ModWindow.Close();
        yield return null;
        yield return ctx.Screenshot("note-popout");
        ModWindow.Open("notes");

        Popouts.DockBack(first);
        yield return null;
        bool docked = !Popouts.IsOpen(first) && ModWindow.IsOpen && ModWindow.CurrentTab == "notes";
        string again = NotesPopout.PopOut(note);
        if (again != null && !created.Contains(again)) created.Add(again);
        d = $"docked={docked} first={first} again={again}";
        if (docked && again == first) ctx.Pass("popout-dock", d);
        else ctx.Fail("popout-dock", d);

        NotesPopout.Forget(note);
        bool gone = true;
        foreach (var id in Popouts.Windows(NotesPopout.KindId)) gone &= id != first;
        d = $"gone={gone} open={Popouts.IsOpen(first)}";
        if (gone && !Popouts.IsOpen(first)) ctx.Pass("popout-delete", d);
        else ctx.Fail("popout-delete", d);
        ModWindow.Close();
    }
}

public sealed class NoteTab
{
    public string name;
    public string text;
    // Stable across renames and moves, so a pop-out keeps its note.
    public string id = Guid.NewGuid().ToString("N").Substring(0, 8);
}

public static class NotesStore
{
    public static readonly List<NoteTab> Tabs = new();
    // Goes up whenever the notes change (saved or loaded), so a note's pop-out knows to redraw.
    public static int Version { get; private set; }

    public static string FilePath => Paths.ConfigPath + "/IgiQoL/notes.json";

    // A line for the Notes tab when notes.json couldn't be read at load; null otherwise.
    public static string Problem { get; private set; }
    // The file exists but couldn't be read (locked, no permission): it's kept, and nothing is written over it.
    public static bool ReadOnly { get; private set; }

    public static void Load()
    {
        Tabs.Clear();
        Problem = null;
        ReadOnly = false;
        string path = FilePath;
        if (File.Exists(path))
        {
            string text = null;
            try { text = File.ReadAllText(path); }
            catch (Exception e)   // locked by another program, or unreadable: keep the file and never write over it
            {
                ReadOnly = true;
                // Forward slashes: TMP reads the "\n" of a Windows path like ...\notes.json as a line break.
                Problem = $"notes.json couldn't be read ({e.Message}). Your notes are still in the file; edits here won't be saved. Fix the file and restart the game.".Replace('\\', '/');
                Plugin.Log.LogWarning("[Notes] " + Problem);
            }
            if (text != null)
            {
                try
                {
                    var root = JsonValue.Parse(text);
                    if (!root.IsJsonObject || !root.AsJsonObject["tabs"].IsJsonArray) throw new FormatException("no tabs array");
                    foreach (var item in root.AsJsonObject["tabs"].AsJsonArray)
                    {
                        if (!item.IsJsonObject) continue;
                        var o = item.AsJsonObject;
                        var tab = new NoteTab { name = o["name"].AsString ?? "Notes", text = o["text"].AsString ?? "" };
                        if (o["id"].IsString) tab.id = o["id"].AsString;
                        Tabs.Add(tab);
                    }
                }
                catch (Exception e)
                {
                    Tabs.Clear();
                    string moved = path + ".broken-" + DateTime.UtcNow.Ticks;
                    try { File.Move(path, moved); }
                    catch (Exception e2) { moved = path + " (rename failed: " + e2.Message + ")"; }
                    Plugin.Log.LogWarning($"[Notes] could not read notes.json ({e.Message}); moved to {moved}");
                    Problem = $"notes.json couldn't be read ({e.Message}) and was moved to {moved}. Starting with an empty note.".Replace('\\', '/');
                }
            }
        }
        if (Tabs.Count == 0) Tabs.Add(new NoteTab { name = "Notes", text = "" });
        Version++;
    }

    public static void Save()
    {
        Version++;
        if (ReadOnly) { Plugin.Log.LogWarning("[Notes] not saved: notes.json couldn't be read at startup"); return; }
        try
        {
            var array = new JsonArray();
            foreach (var t in Tabs)
                array.Add(new JsonObject().Add("id", t.id).Add("name", t.name).Add("text", t.text));
            string json = new JsonObject().Add("tabs", array).ToString(true);
            string path = FilePath;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temp = path + ".tmp";
            File.WriteAllText(temp, json);
            if (File.Exists(path)) File.Replace(temp, path, null);
            else File.Move(temp, path);
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"[Notes] could not save notes.json: {e.Message}");
        }
    }
}

public sealed class NotesTab : MonoBehaviour
{
    private const string PrefTab = "IgiQoL.Notes.Tab";
    private const float BodyHeight = 420f;
    private const string Placeholder = "Write notes here. # heading, **bold**, *italic*, - bullet, [text](url) or a bare https:// link.";

    // null = default: edit while the note is empty, view otherwise. Reset when the selected note changes.
    private static bool? editingOverride;

    public TMP_InputField editor;
    public TMP_Text viewer;
    private NoteTab note;

    private static int Current
    {
        get => Mathf.Clamp(PlayerPrefs.GetInt(PrefTab, 0), 0, NotesStore.Tabs.Count - 1);
        set => PlayerPrefs.SetInt(PrefTab, Mathf.Clamp(value, 0, NotesStore.Tabs.Count - 1));
    }

    public static void Build(RectTransform content)
    {
        var tab = content.gameObject.AddComponent<NotesTab>();
        tab.BuildUi(content);
    }

    private void BuildUi(RectTransform content)
    {
        int current = Current;
        note = NotesStore.Tabs[current];
        bool editing = editingOverride ?? string.IsNullOrEmpty(note.text);

        var bar = Kit.Row(content, 4f, 24f);
        for (int i = 0; i < NotesStore.Tabs.Count; i++)
        {
            int index = i;
            var button = Kit.TextButton(bar, NotesStore.Tabs[i].name, () => Select(index), 100f);
            ((Image)button.targetGraphic).color = i == current ? ColorHelper.mapDockButtonActive : ColorHelper.panelButton;
        }
        Kit.TextTile(bar, "+", 22f, AddNote);
        CaptainCards.AddSpacer(bar);
        Kit.TextTile(bar, "<", 22f, () => Move(-1));
        Kit.TextTile(bar, ">", 22f, () => Move(1));
        Kit.TextButton(bar, "Rename", Rename, 70f);
        var delete = Kit.TextButton(bar, "Delete", Delete, 70f);
        delete.interactable = NotesStore.Tabs.Count > 1;
        Kit.TextButton(bar, editing ? "View" : "Edit", () => SetEditing(!editing), 70f);
        var shown = note;
        PopoutTabButton.Add(bar, "Pop out", "Dock back", () => NotesPopout.IsOut(shown), () => NotesPopout.PopOut(shown), () => NotesPopout.DockBack(shown), 80f,
            "This note in a window of its own beside the game, read-only. You edit it here.");
        if (NotesStore.Problem != null) Kit.Muted(content, NotesStore.Problem);

        if (editing) BuildEditor(content);
        else BuildViewer(content);
    }

    private void BuildEditor(RectTransform content)
    {
        var field = UiSearchField.Build(content, Placeholder, 13f);
        field.lineType = TMP_InputField.LineType.MultiLineNewline;
        field.characterLimit = 0;
        field.textComponent.alignment = TextAlignmentOptions.TopLeft;
        field.textComponent.textWrappingMode = TextWrappingModes.Normal;
        field.richText = false;
        field.placeholder.GetComponent<TMP_Text>().alignment = TextAlignmentOptions.TopLeft;
        field.placeholder.GetComponent<TMP_Text>().textWrappingMode = TextWrappingModes.Normal;
        var font = EditorFont();
        if (font != null) field.fontAsset = font;
        field.SetTextWithoutNotify(note.text);
        field.onEndEdit.AddListener(_ => Commit());
        var le = field.gameObject.AddComponent<LayoutElement>();
        le.preferredHeight = BodyHeight;
        le.flexibleWidth = 1f;
        editor = field;
    }

    // The game's pixel fonts draw '*' as an empty glyph, which hides the markdown markers while typing.
    // The editor uses the game's Lato font instead when it is loaded; the viewer keeps the game font.
    private static TMP_FontAsset editorFont;

    private static TMP_FontAsset EditorFont()
    {
        if (editorFont != null) return editorFont;
        foreach (var f in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
            if (f.name == "Lato-Regular SDF") { editorFont = f; break; }
        return editorFont;
    }

    private void BuildViewer(RectTransform content)
    {
        var inner = Kit.Scroll(content);
        var scrollLe = inner.GetComponentInParent<ScrollRect>().GetComponent<LayoutElement>();
        scrollLe.flexibleHeight = 0f;
        scrollLe.preferredHeight = BodyHeight;
        var label = UiCards.AddLabel(inner, NotesMarkdown.Render(note.text), 13f, ColorHelper.offWhite);
        label.raycastTarget = true;
        label.richText = true;
        label.gameObject.AddComponent<LinkClicks>();
        viewer = label;
    }

    // Writes the editor text into the note when it changed; returns true when it did.
    private bool Commit()
    {
        if (editor == null || note == null || editor.text == note.text) return false;
        note.text = editor.text;
        NotesStore.Save();
        return true;
    }

    private void OnDestroy()
    {
        Commit();
        editor = null;
    }

    private void Rebuild() => ModWindow.Show("notes");

    private void Select(int index)
    {
        try
        {
            Commit();
            if (index != Current) editingOverride = null;
            Current = index;
            Rebuild();
        }
        catch (Exception e) { Notes.Instance.Fail(e, nameof(Select)); }
    }

    private void SetEditing(bool value)
    {
        try
        {
            Commit();
            editingOverride = value;
            Rebuild();
        }
        catch (Exception e) { Notes.Instance.Fail(e, nameof(SetEditing)); }
    }

    private void AddNote()
    {
        try
        {
            Commit();
            NotesStore.Tabs.Add(new NoteTab { name = "Note " + (NotesStore.Tabs.Count + 1), text = "" });
            Current = NotesStore.Tabs.Count - 1;
            editingOverride = null;
            NotesStore.Save();
            Rebuild();
        }
        catch (Exception e) { Notes.Instance.Fail(e, nameof(AddNote)); }
    }

    private void Move(int delta)
    {
        try
        {
            Commit();
            int from = Current, to = from + delta;
            if (to < 0 || to >= NotesStore.Tabs.Count) return;
            (NotesStore.Tabs[from], NotesStore.Tabs[to]) = (NotesStore.Tabs[to], NotesStore.Tabs[from]);
            Current = to;
            NotesStore.Save();
            Rebuild();
        }
        catch (Exception e) { Notes.Instance.Fail(e, nameof(Move)); }
    }

    private void Rename()
    {
        try
        {
            Commit();
            var target = note;
            AlertPopup.ShowInput("Tab name", name =>
            {
                try
                {
                    name = (name ?? "").Trim();
                    if (name.Length == 0) return;
                    target.name = name;
                    NotesStore.Save();
                    NotesPopout.Retitle(target);
                    if (ModWindow.IsOpen && ModWindow.CurrentTab == "notes") ModWindow.Show("notes");
                }
                catch (Exception e) { Notes.Instance.Fail(e, nameof(Rename)); }
            }, "Rename", target.name);
        }
        catch (Exception e) { Notes.Instance.Fail(e, nameof(Rename)); }
    }

    private void Delete()
    {
        try
        {
            Commit();
            if (NotesStore.Tabs.Count <= 1) return;
            var target = note;
            AlertPopup.ShowQuery("Delete '" + target.name + "'?", "Delete", "Keep", onYes: () =>
            {
                try
                {
                    int at = NotesStore.Tabs.IndexOf(target);
                    if (at < 0) return;
                    NotesStore.Tabs.RemoveAt(at);
                    NotesPopout.Forget(target);
                    Current = Mathf.Min(Current, NotesStore.Tabs.Count - 1);
                    editingOverride = null;
                    NotesStore.Save();
                    if (ModWindow.IsOpen && ModWindow.CurrentTab == "notes") ModWindow.Show("notes");
                }
                catch (Exception e) { Notes.Instance.Fail(e, nameof(Delete)); }
            });
        }
        catch (Exception e) { Notes.Instance.Fail(e, nameof(Delete)); }
    }
}
