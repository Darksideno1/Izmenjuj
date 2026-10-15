namespace Izmenjuj.Web.Services;

public sealed record MatchInput(string UserId, IReadOnlySet<Guid> Wanted, IReadOnlySet<Guid> ForTrade);

public sealed record MatchResult(
    string UserId,
    IReadOnlyList<Guid> YouGive,
    IReadOnlyList<Guid> YouReceive,
    int Score,
    int BalancedTradeCount);

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
                var youGive = me.ForTrade.Intersect(candidate.Wanted).OrderBy(x => x).ToArray();
                var youReceive = candidate.ForTrade.Intersect(me.Wanted).OrderBy(x => x).ToArray();
                var balancedTradeCount = Math.Min(youGive.Length, youReceive.Length);

                // Balanced two-way trades rank highest. Larger useful pools break ties.
                var score = balancedTradeCount * 1000
                            + (youGive.Length + youReceive.Length) * 10
                            - Math.Abs(youGive.Length - youReceive.Length);

                return new MatchResult(
                    candidate.UserId,
                    youGive,
                    youReceive,
                    score,
                    balancedTradeCount);
            })
            .Where(match => match.YouGive.Count > 0 && match.YouReceive.Count > 0)
            .OrderByDescending(match => match.Score)
            .ThenByDescending(match => match.BalancedTradeCount)
            .ToArray();
    }
}
