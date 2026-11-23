using System;
using MelonLoader;
using TMPro;
using UnityEngine;

namespace YargAccess
{
    // YARG's main menu shows a "Message of the Day" panel (what is new in this version), drawn only. A key
    // reads it: F2, RT on a controller, hold Select and strum up on a guitar. It works in
    // every menu; the text is kept once the main menu has been seen.
    public static class WhatsNew
    {
        static string _text;

        public static string Text()
        {
            try
            {
                foreach (var t in Resources.FindObjectsOfTypeAll<TMP_Text>())
                {
                    if (t == null || t.transform.parent == null || t.transform.parent.name != "Message of the Day") continue;
                    if (!t.gameObject.scene.IsValid()) continue;
                    var s = TextUtil.Clean(t.text);
                    if (!string.IsNullOrWhiteSpace(s)) _text = s;
                    break;
                }
            }
            catch (Exception ex) { ModLog.Warn("WhatsNew.Text", ex); }
            return _text;
        }

        public static void Read()
        {
            var s = Text();
            if (string.IsNullOrEmpty(s)) { MenuAnnouncer.SayShown(Loc.T("No message from YARG yet. It shows on the main menu.")); return; }
            MenuAnnouncer.Say(Loc.F("YARG's message: {0}", s));
        }
    }
}
