namespace Izmenjuj.Web.Services;

public sealed record MatchInput(string UserId, IReadOnlySet<Guid> Wanted, IReadOnlySet<Guid> ForTrade);
public sealed record MatchResult(string UserId, IReadOnlyList<Guid> YouGive, IReadOnlyList<Guid> YouReceive, int Score);

public interface IMatchingService
{
    IReadOnlyList<MatchResult> FindMatches(MatchInput me, IEnumerable<MatchInput> candidates);
}

public sealed class MatchingService : IMatchingService
{
    public IReadOnlyList<MatchResult> FindMatches(MatchInput me, IEnumerable<MatchInput> candidates)
    {
        return candidates
            .Where(candidate => candidate.UserId != me.UserId)
            .Select(candidate =>
            {
                var youGive = me.ForTrade.Intersect(candidate.Wanted).ToArray();
                var youReceive = candidate.ForTrade.Intersect(me.Wanted).ToArray();
                var score = Math.Min(youGive.Length, youReceive.Length) * 100
                            + youGive.Length + youReceive.Length;
                return new MatchResult(candidate.UserId, youGive, youReceive, score);
            })
            .Where(match => match.YouGive.Count > 0 && match.YouReceive.Count > 0)
            .OrderByDescending(match => match.Score)
            .ToArray();
    }
}
