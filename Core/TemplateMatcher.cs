namespace Vrec;

public sealed record VisualTemplate(MatchState State, double X, double Y, double Width, double Height, byte[] Pixels);
public static class TemplateMatcher
{
    public const int SampleWidth = 32, SampleHeight = 18;
    public static double Similarity(byte[] a, byte[] b)
    {
        if (a.Length != SampleWidth * SampleHeight * 3 || b.Length != a.Length) return 0;
        long difference = 0;
        for (int i = 0; i < a.Length; i++) difference += Math.Abs(a[i] - b[i]);
        return 1 - difference / (255d * a.Length);
    }
    public static MatchState Classify(IEnumerable<(MatchState State, double Score)> scores, double threshold, double margin)
    {
        var best = scores.GroupBy(x => x.State).Select(g => (State: g.Key, Score: g.Max(x => x.Score)))
            .OrderByDescending(x => x.Score).ToArray();
        if (best.Length == 0 || best[0].Score < threshold || (best.Length > 1 && best[0].Score - best[1].Score < margin)) return MatchState.Unknown;
        return best[0].State;
    }
}
