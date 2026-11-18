using System;
using System.Collections.Generic;
using MelonLoader;
using YARG.Core.Chart;
using YARG.Gameplay;
using YargAccess.Audio;

namespace YargAccess
{
    public static class BeatTicks
    {
        static GameManager _gm;
        static List<Beatline> _beats;
        static int _bi;
        static double _lastTime = double.NaN;

        public static void Reset()
        {
            _gm = null;
            _beats = null;
            _bi = 0;
            _lastTime = double.NaN;
        }

        public static void Tick()
        {
            if (ModSettings.BeatTicks == null || !ModSettings.BeatTicks.Value) return;
            if (!CuePlayer.Ready || !CueBank.EnsureLoaded()) return;
            try
            {
                if (_gm == null)
                {
                    _beats = null;
                    _gm = UnityEngine.Object.FindObjectOfType<GameManager>();
                    if (_gm == null) return;
                }
                if (_beats == null)
                {
                    var chart = _gm.Chart;
                    if (chart == null) return;
                    _beats = chart.SyncTrack.Beatlines;
                    _lastTime = double.NaN;
                }
                double now = GameplayContext.CueTime(_gm);
                if (double.IsNaN(_lastTime) || now < _lastTime - 0.05 || now > _lastTime + 0.5)
                {
                    _bi = 0;
                    while (_bi < _beats.Count && _beats[_bi].Time <= now) _bi++;
                }
                _lastTime = now;
                while (_bi < _beats.Count && _beats[_bi].Time <= now)
                {
                    var b = _beats[_bi];
                    if (b.Time > now - 0.05)
                    {
                        if (b.Type == BeatlineType.Measure) CueBank.Play("measure", 0.8f);
                        else if (b.Type == BeatlineType.Strong) CueBank.Play("beat", 0.8f);
                    }
                    _bi++;
                }
            }
            catch (NullReferenceException) { }
            catch (Exception ex) { ModLog.Warn("BeatTicks.Tick", ex); }
        }
    }
}
