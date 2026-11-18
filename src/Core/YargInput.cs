using System;
using System.Reflection;
using UnityEngine;
using MelonLoader;
using YARG.Menu.Navigation;

namespace YargAccess
{
    public static class YargInput
    {
        const BindingFlags BF = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;
        static Type _ncType;
        static Type _maType;
        static FieldInfo _schemeStackF;

        public static bool Invoke(string menuAction, bool hold = false)
        {
            try
            {
                var nav = UnityEngine.Object.FindObjectOfType<Navigator>();
                if (nav == null) return false;

                if (_schemeStackF == null) _schemeStackF = typeof(Navigator).GetField("_schemeStack", BF);
                var stackObj = _schemeStackF != null ? _schemeStackF.GetValue(nav) : null;
                if (stackObj == null) return false;

                var stackType = stackObj.GetType();
                var count = (int)stackType.GetProperty("Count").GetValue(stackObj, null);
                if (count == 0) return false;
                var scheme = stackType.GetMethod("Peek").Invoke(stackObj, null);
                if (scheme == null) return false;

                if (_ncType == null) _ncType = Type.GetType("YARG.Menu.Navigation.NavigationContext, Assembly-CSharp");
                if (_maType == null) _maType = _ncType.GetField("Action").FieldType;

                var actionVal = Enum.Parse(_maType, menuAction);
                var ctx = Activator.CreateInstance(_ncType, new object[] { actionVal, null, false });
                var st = scheme.GetType();
                if (!hold) st.GetMethod("InvokeFuncs").Invoke(scheme, new object[] { ctx });

                if (!hold)
                {
                    var off = st.GetMethod("InvokeHoldOffFuncs");
                    if (off != null) off.Invoke(scheme, new object[] { ctx });
                }
                else
                {
                    var h = st.GetMethod("InvokeHoldFuncs");
                    if (h != null) h.Invoke(scheme, new object[] { ctx });
                }
                return true;
            }
            catch (Exception ex)
            {
                ModLog.Warn("YargInput.Invoke(" + menuAction + ")", ex);
                return false;
            }
        }

        public static bool HasHold(string menuAction, out float seconds)
        {
            seconds = 0f;
            try
            {
                var nav = UnityEngine.Object.FindObjectOfType<Navigator>();
                if (nav == null) return false;
                if (_schemeStackF == null) _schemeStackF = typeof(Navigator).GetField("_schemeStack", BF);
                var stackObj = _schemeStackF != null ? _schemeStackF.GetValue(nav) : null;
                if (stackObj == null) return false;
                var stackType = stackObj.GetType();
                if ((int) stackType.GetProperty("Count").GetValue(stackObj, null) == 0) return false;
                var scheme = stackType.GetMethod("Peek").Invoke(stackObj, null);
                if (scheme == null) return false;

                if (_maType == null)
                {
                    if (_ncType == null) _ncType = Type.GetType("YARG.Menu.Navigation.NavigationContext, Assembly-CSharp");
                    _maType = _ncType.GetField("Action").FieldType;
                }
                var m = scheme.GetType().GetMethod("TryGetHoldSeconds");
                if (m == null) return false;
                var args = new object[] { Enum.Parse(_maType, menuAction), 0f };
                bool ok = (bool) m.Invoke(scheme, args);
                if (ok) seconds = (float) args[1];
                return ok;
            }
            catch { return false; }
        }
    }
}
