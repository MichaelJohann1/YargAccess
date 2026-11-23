using System;
using YARG.Player;

namespace YargAccess
{
    // Whose part makes note sounds: one player only, player one. That is the first
    // live human in the band's order, else the first live bot, else (watching a replay) the replay's
    // first human, else its first bot. In Play With Replay the replay's players are never player one
    // while anyone plays live. The other players play without note sounds.
    public static class SoundOwner
    {
        public static bool Skip(YargPlayer p)
        {
            if (p == null || p.Profile == null) return true;
            try
            {
                var one = PlayerOne();
                return one != null && !ReferenceEquals(one, p);
            }
            catch (Exception ex) { ModLog.Warn("SoundOwner.Skip", ex); return false; }
        }

        public static YargPlayer PlayerOne()
        {
            var gm = GameplayContext.Manager;
            if (gm == null) return null;
            YargPlayer best = null;
            int bestRank = -1;
            foreach (var bp in gm.Players)
            {
                var q = bp != null ? bp.Player : null;
                if (q == null || q.Profile == null) continue;
                int rank = (q.IsReplay ? 0 : 2) + (q.Profile.IsBot ? 0 : 1);
                if (rank > bestRank) { best = q; bestRank = rank; }
            }
            return best;
        }
    }
}
