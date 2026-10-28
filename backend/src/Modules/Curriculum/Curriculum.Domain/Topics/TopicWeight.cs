namespace Nirengi.Modules.Curriculum.Domain.Topics;

/// <summary>How often a topic is asked, from our own tagging of past ÖSYM booklets (not an official figure).</summary>
public enum WeightTier
{
    /// <summary>Asked in fewer than half of the counted years.</summary>
    Rare = 0,
    Low = 1,

    /// <summary>At least 0.8 questions a year.</summary>
    Medium = 2,

    /// <summary>At least 2 questions a year.</summary>
    High = 3,
}

/// <summary>
/// Expected questions of a topic for one exam year (q_i): the 2022–2026 mean, normalised to the test total.
/// Versioned by exam year so a new year's weights never rewrite an older analysis.
/// </summary>
public sealed class TopicWeight
{
    private TopicWeight()
    {
    }

    public Guid TopicId { get; private set; }

    public int ExamYear { get; private set; }

    public decimal ExpectedQuestions { get; private set; }

    public WeightTier Tier { get; private set; }

    /// <summary>Years the topic was asked at least once, out of <see cref="YearsCounted"/>.</summary>
    public int YearsAsked { get; private set; }

    public int YearsCounted { get; private set; }

    public static TopicWeight Create(Guid topicId, int examYear, decimal expectedQuestions, WeightTier tier, int yearsAsked, int yearsCounted)
    {
        var weight = new TopicWeight { TopicId = topicId, ExamYear = examYear };
        weight.Update(expectedQuestions, tier, yearsAsked, yearsCounted);
        return weight;
    }

    public void Update(decimal expectedQuestions, WeightTier tier, int yearsAsked, int yearsCounted)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(expectedQuestions);
        if (yearsAsked < 0 || yearsAsked > yearsCounted)
        {
            throw new ArgumentOutOfRangeException(nameof(yearsAsked));
        }

        ExpectedQuestions = expectedQuestions;
        Tier = tier;
        YearsAsked = yearsAsked;
        YearsCounted = yearsCounted;
    }
}
