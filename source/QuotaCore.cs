using System;
using System.Collections.Generic;
using System.Web.Script.Serialization;

public sealed class QuotaWindow {
    public double Remaining;
    public DateTimeOffset? Reset;
    public string Percent { get { return Remaining.ToString("0.#") + "%"; } }
}
public sealed class QuotaSnapshot {
    public QuotaWindow FiveHour, Weekly;
    internal static Dictionary<string, object> Obj(object o) { return o as Dictionary<string, object>; }
    internal static object Get(Dictionary<string, object> d, string k) { object v; return d != null && d.TryGetValue(k, out v) ? v : null; }
    public static QuotaSnapshot Parse(string json) {
        var root = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json);
        var buckets = Obj(Get(root, "rateLimitsByLimitId"));
        var bucket = buckets != null ? Obj(Get(buckets, "codex")) : Obj(Get(root, "rateLimits"));
        var snapshot = new QuotaSnapshot();
        foreach (var key in new [] {"primary", "secondary"}) {
            var w = Obj(Get(bucket, key));
            var used = Get(w, "usedPercent"); var duration = Get(w, "windowDurationMins");
            if (used == null || duration == null) continue;
            double n = Convert.ToDouble(used); if (double.IsNaN(n) || double.IsInfinity(n)) continue;
            var item = new QuotaWindow { Remaining = Math.Max(0, Math.Min(100, 100 - n)) };
            var reset = Get(w, "resetsAt");
            if (reset != null) { try { item.Reset = DateTimeOffset.FromUnixTimeSeconds(Convert.ToInt64(reset)); } catch (ArgumentOutOfRangeException) {} }
            int mins = Convert.ToInt32(duration);
            if (mins == 300) snapshot.FiveHour = item;
            if (mins == 10080) snapshot.Weekly = item;
        }
        return snapshot;
    }
}
