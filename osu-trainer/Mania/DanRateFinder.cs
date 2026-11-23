using FsBeatmapProcessor;
using System;
using System.Collections.Concurrent;
using System.Threading;

namespace osu_trainer.Mania
{
    public enum DanRateOutcome
    {
        /// <summary>The map reaches the dan at <see cref="DanRateResult.Rate"/>.</summary>
        Reached,
        /// <summary>Even at the lowest rate tried the map is past the dan: it would need a lower rate.</summary>
        NeedsLowerRate,
        /// <summary>Even at the highest rate tried the map is below the dan: it would need a higher rate.</summary>
        NeedsHigherRate,
        /// <summary>The map couldn't be rated; <see cref="DanRateResult.Error"/> says why.</summary>
        Failed,
    }

    /// <summary>What a dan button's search found.</summary>
    public sealed class DanRateResult
    {
        public DanRateOutcome Outcome { get; internal set; }
        public DanLevel Target { get; internal set; }

        /// <summary>
        /// Reached: the rate to set. NeedsLowerRate / NeedsHigherRate: the end of the range that was tried.
        /// Failed: the rate that couldn't be rated.
        /// </summary>
        public decimal Rate { get; internal set; }

        /// <summary>The rating at <see cref="Rate"/> (for Failed, the failed rating).</summary>
        public ManiaRating Rating { get; internal set; }

        public string Error { get; internal set; }

        /// <summary>How many rates the search looked at (some may have come from the cache).</summary>
        public int Steps { get; internal set; }
    }

    /// <summary>
    /// The search behind the dan buttons. It looks for the lowest rate, in 0.01x steps from 0.50x to 3.00x, at which
    /// the map rates as the dan or harder, while 0.01x less rates below it: the rate at which the rating crosses the
    /// dan's threshold (what the average chart of that dan rates; see <see cref="DanTable"/>).
    /// Ratings nearly always grow with the rate, so there is normally one such crossing. They can dip a little (with
    /// Full LN, a 4th dan chart rates 0.05 lower at 1.04x than at 1.03x); if a dip straddles the threshold, the search
    /// still returns a crossing, just not necessarily the lowest one.
    /// </summary>
    public static class DanRateFinder
    {
        // Rates in hundredths: 100 is 1.00x.
        public const int MinStep = 50;   // 0.50x, the low end of the rate slider
        public const int MaxStep = 300;  // 3.00x (soundstretch takes tempo changes up to +5000%)

        public static decimal MinRate => MinStep / 100M;
        public static decimal MaxRate => MaxStep / 100M;

        /// <summary>
        /// Ratings grow roughly as rate^0.92 across the 4K dan charts (one 4th dan chart: 38.25 at 1.0x, 45.20 at 1.2x).
        /// Only used to pick which rates to try first (for 7K too); every answer is checked by rating it.
        /// </summary>
        public const double TypicalExponent = 0.92;

        // After this many model-guided steps the search falls back to plain bisection, which always finishes.
        private const int GuidedSteps = 6;

        /// <summary>
        /// True when the rating counts as <paramref name="target"/> or a harder dan of the same table (rounded the way
        /// the window shows it). A rating from the other key mode's calculator never reaches it.
        /// </summary>
        public static bool Reaches(ManiaRating rating, DanLevel target) =>
            rating?.Status == ManiaRatingStatus.Ok && rating.Dan != null
            && ReferenceEquals(rating.Dan.System, target.System) && rating.Dan.Index >= target.Index;

        /// <param name="ratingAt">The map's rating at a rate in hundredths.</param>
        /// <param name="target">The dan to reach.</param>
        /// <param name="guess">Where to start, in hundredths (clamped to the range).</param>
        /// <param name="token">Checked between ratings.</param>
        public static DanRateResult Find(Func<int, ManiaRating> ratingAt, DanLevel target, int guess, CancellationToken token)
        {
            if (ratingAt == null)
                throw new ArgumentNullException(nameof(ratingAt));
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            // lo: the highest step rated below the dan; hi: the lowest step rated at or above it.
            // Both start just outside the range, where nothing has been rated.
            int lo = MinStep - 1, hi = MaxStep + 1;
            ManiaRating loRating = null, hiRating = null;
            int steps = 0;
            int next = guess;
            while (hi - lo > 1)
            {
                token.ThrowIfCancellationRequested();
                int step = Clamp(next, lo + 1, hi - 1);
                var rating = ratingAt(step);
                steps++;
                // a rating read against the other key mode's dans can't be compared with this dan at all
                bool otherTable = rating?.Status == ManiaRatingStatus.Ok && !ReferenceEquals(rating.System, target.System);
                if (rating == null || rating.Status != ManiaRatingStatus.Ok || otherTable)
                {
                    return new DanRateResult
                    {
                        Outcome = DanRateOutcome.Failed,
                        Target = target,
                        Rate = step / 100M,
                        Rating = rating,
                        Error = otherTable
                            ? $"{target.DisplayName} is a {target.System.Keys}K dan and this is a {rating.Keys}K map"
                            : rating?.Error ?? "no rating",
                        Steps = steps,
                    };
                }
                if (Reaches(rating, target))
                {
                    hi = step;
                    hiRating = rating;
                }
                else
                {
                    lo = step;
                    loRating = rating;
                }
                next = steps < GuidedSteps ? Predict(target, lo, loRating, hi, hiRating) : lo + (hi - lo) / 2;
            }

            if (hi > MaxStep)
                return Result(DanRateOutcome.NeedsHigherRate, target, MaxStep, loRating, steps);
            // nothing in the range was below the dan, and the lowest rate is already past it
            if (lo < MinStep && hiRating.Dan.Index > target.Index)
                return Result(DanRateOutcome.NeedsLowerRate, target, MinStep, hiRating, steps);
            return Result(DanRateOutcome.Reached, target, hi, hiRating, steps);
        }

        private static DanRateResult Result(DanRateOutcome outcome, DanLevel target, int step, ManiaRating rating, int steps) =>
            new DanRateResult { Outcome = outcome, Target = target, Rate = step / 100M, Rating = rating, Steps = steps };

        // The first step at which the rating should reach the dan's threshold: on a log-log line through the rated
        // steps either side of the crossing, or, with only one side rated so far, through it with the typical slope.
        private static int Predict(DanLevel target, int lo, ManiaRating loRating, int hi, ManiaRating hiRating)
        {
            double threshold = DanTable.RoundForDisplay(target.Threshold);
            double x;
            if (loRating != null && hiRating != null)
            {
                double slope = Math.Log(Math.Max(hiRating.Rating, 0.01) / Math.Max(loRating.Rating, 0.01)) / Math.Log((double)hi / lo);
                x = slope > 0.05 && !double.IsInfinity(slope)
                    ? lo * Math.Pow(threshold / Math.Max(loRating.Rating, 0.01), 1 / slope)
                    : lo + (hi - lo) / 2.0;
            }
            else
            {
                var (step, rating) = loRating != null ? (lo, loRating.Rating) : (hi, hiRating.Rating);
                x = step * Math.Pow(threshold / Math.Max(rating, 0.01), 1 / TypicalExponent);
            }
            if (double.IsNaN(x))
                return lo + (hi - lo) / 2;
            // the caller keeps it strictly between lo and hi, so a prediction of hi checks hi - 1
            return (int)Math.Ceiling(Math.Max(MinStep - 1, Math.Min(MaxStep + 1, x)) - 1e-9);
        }

        private static int Clamp(int value, int min, int max) => value < min ? min : value > max ? max : value;
    }

    /// <summary>
    /// The ratings of one map, with one long note conversion, at each rate a dan button's search has tried. The
    /// editor keeps one for the loaded map, so pressing a dan button again, or another one, reuses them.
    /// Safe to use from several threads.
    /// </summary>
    public sealed class DanRateCache
    {
        private readonly Beatmap map;     // the editor's map; only compared by reference
        private readonly Beatmap source;  // a private copy, which nothing edits
        private readonly ConcurrentDictionary<int, ManiaRating> ratings = new ConcurrentDictionary<int, ManiaRating>();

        /// <summary>Create on the thread that owns <paramref name="map"/>: it takes a copy.</summary>
        public DanRateCache(Beatmap map, LnConversion conversion)
        {
            this.map = map ?? throw new ArgumentNullException(nameof(map));
            source = new Beatmap(map);
            Conversion = conversion;
        }

        public LnConversion Conversion { get; }

        public bool IsFor(Beatmap other, LnConversion conversion) => ReferenceEquals(map, other) && conversion == Conversion;

        /// <summary>
        /// The rating at a rate in hundredths, worked out the way the editor rates the map it shows (SetRate, then the
        /// long note conversion and the calculator), so the window shows the same rating once the rate is set. Never throws.
        /// </summary>
        public ManiaRating RatingAt(int step) => ratings.GetOrAdd(step, s =>
        {
            try
            {
                var copy = new Beatmap(source);
                copy.SetRate(s / 100M);
                return ManiaDifficulty.Calculate(copy, Conversion);
            }
            catch (Exception e)
            {
                // Calculate never throws, but the copy or SetRate might on a broken map; the search then ends with
                // "No rating" instead of an exception in a dan button's click handler
                return ManiaRating.Failed(e.Message, ManiaDifficulty.KeyCount(source));
            }
        });
    }
}
