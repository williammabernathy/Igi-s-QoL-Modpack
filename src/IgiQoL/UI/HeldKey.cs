using Behaviour.UI;
using UnityEngine.InputSystem;

namespace IgiQoL.UI;

// Whether a configured key is held right now, for settings that name a key to hold.
internal static class HeldKey
{
    // False while the player types in a text field. The Input System has no single Ctrl, Shift or Alt key, so either
    // side counts for those.
    internal static bool IsDown(Key key)
    {
        var kb = Keyboard.current;
        if (kb == null || UIHelper.IsTypingInInputField()) return false;
        switch (key)
        {
            case Key.LeftCtrl or Key.RightCtrl: return kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed;
            case Key.LeftShift or Key.RightShift: return kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
            case Key.LeftAlt or Key.RightAlt: return kb.leftAltKey.isPressed || kb.rightAltKey.isPressed;
            case var k when k > Key.None && (int)k <= Keyboard.KeyCount: return kb[k].isPressed;
            default: return false;
        }
    }
}
