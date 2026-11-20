namespace Hardening.Specs;

/// <summary>The shrinking that the step 2 expressions and the step 3 mock sequences share.</summary>
public static class Shrinker
{
    /// <summary>
    /// Replaces a failing case with a smaller one while one still fails the same way. Each round runs
    /// every one-step reduction of the current case in one batch and keeps the smallest that fails,
    /// until no reduction fails.
    /// </summary>
    /// <param name="failing">The result of the failing case.</param>
    /// <param name="reductions">The one-step reductions of a result's case.</param>
    /// <param name="run">Runs a batch of cases.</param>
    /// <param name="keeps">True when a result fails the same way as <paramref name="failing"/>.</param>
    /// <param name="size">The size a smaller result has less of.</param>
    public static async Task<TResult> ShrinkAsync<TCase, TResult>(
        TResult failing,
        Func<TResult, IEnumerable<TCase>> reductions,
        Func<IReadOnlyList<TCase>, Task<IReadOnlyList<TResult>>> run,
        Func<TResult, bool> keeps,
        Func<TResult, (int, int)> size)
        where TResult : class
    {
        var current = failing;
        while (true)
        {
            var candidates = reductions(current).ToList();
            if (candidates.Count == 0)
            {
                return current;
            }

            var smaller = (await run(candidates)).Where(keeps).MinBy(size);
            if (smaller is null)
            {
                return current;
            }

            current = smaller;
        }
    }
}
