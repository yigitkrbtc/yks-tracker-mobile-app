using Nirengi.Modules.Curriculum.Domain.Exams;
using Nirengi.Modules.Curriculum.Domain.Topics;
using Nirengi.Modules.Curriculum.Infrastructure.Seed;
using Shouldly;

namespace Nirengi.Curriculum.UnitTests;

/// <summary>Guards the shipped seed itself: a broken data/seed file fails here, not at deploy.</summary>
public sealed class CurriculumSeedTests
{
    private static readonly CurriculumSeed Seed = CurriculumSeed.LoadEmbedded();

    [Fact]
    public void Shipped_seed_has_every_topic_with_a_weight()
    {
        Seed.ExamYear.ShouldBe(2027);
        Seed.Topics.Count.ShouldBe(370);
        Seed.Topics.Select(t => t.Code).Distinct().Count().ShouldBe(370);
        Seed.Subjects.Count.ShouldBe(20);
        Seed.Hash.Length.ShouldBe(64);
    }

    [Fact]
    public void Every_subject_has_topics()
    {
        var used = Seed.Topics.Select(t => t.SubjectCode).ToHashSet();
        Seed.Subjects.ShouldAllBe(s => used.Contains(s.Code));
    }

    [Fact]
    public void Every_score_type_sees_all_tyt_tests()
    {
        var tyt = Seed.Tests.Where(t => t.Session == ExamSession.TYT).Select(t => t.Code).ToList();
        tyt.Count.ShouldBe(4);
        Seed.ScoreTypes.Select(s => s.Code).ShouldBe(["SAY", "EA", "SOZ", "DIL"]);
        Seed.ScoreTypes.ShouldAllBe(s => tyt.All(s.TestCodes.Contains));
    }

    [Fact]
    public void Ayt_history_and_geography_count_for_both_social_tests()
    {
        foreach (var code in new[] { "AYT.TAR", "AYT.COG" })
        {
            Seed.Subjects.Single(s => s.Code == code).TestCodes.ShouldBe(["AYT-TDE-SB1", "AYT-SB2"]);
        }
    }

    [Fact]
    public void Topic_fields_are_parsed()
    {
        var integral = Seed.Topics.Single(t => t.Code == "AYT.AMAT.26");
        integral.SubjectCode.ShouldBe("AYT.AMAT");
        integral.Name.ShouldBe("Belirli İntegral ve Uygulamaları");
        integral.ExpectedQuestions.ShouldBe(3.33m);
        integral.Tier.ShouldBe(WeightTier.High);
        (integral.YearsAsked, integral.YearsCounted).ShouldBe((6, 6));
        (integral.GradeMin, integral.GradeMax).ShouldBe((12, 12));

        var ranged = Seed.Topics.First(t => t.GradeMin != t.GradeMax);
        ranged.GradeMax.ShouldBeGreaterThan(ranged.GradeMin);
        Seed.Topics.ShouldAllBe(t => t.Subtopics.Count > 0 && t.Subtopics.All(s => s.Length > 0));
    }

    [Fact]
    public void Topic_of_an_unknown_subject_is_rejected()
    {
        const string structure = """{"examYear":2027,"tests":[{"code":"T","session":"TYT","name":"T","questionCount":1}],"units":[{"code":"TYT.A","name":"A","tests":["T"]}],"scoreTypes":[]}""";
        const string taxonomy = "session,unit,topic_code,topic,subtopics,grade,description\nTYT,B,TYT.B.01,X,y,9,\n";
        const string weights = "topic_code,q_i,tier,years_present\nTYT.B.01,1,az,1/8\n";

        Should.Throw<FormatException>(() => CurriculumSeed.Parse(structure, taxonomy, weights)).Message.ShouldContain("TYT.B");
    }

    [Fact]
    public void Quoted_csv_fields_keep_commas_and_quotes()
    {
        var rows = Csv.Read("a,b\r\n\"x, \"\"y\"\"\",z\r\n");
        rows.Single()["a"].ShouldBe("x, \"y\"");
        rows.Single()["b"].ShouldBe("z");
    }

    [Fact]
    public void Hash_changes_with_content()
    {
        const string structure = """{"examYear":2027,"tests":[{"code":"T","session":"TYT","name":"T","questionCount":1}],"units":[{"code":"TYT.A","name":"A","tests":["T"]}],"scoreTypes":[]}""";
        const string taxonomy = "session,unit,topic_code,topic,subtopics,grade,description\nTYT,A,TYT.A.01,X,y,9,\n";

        var a = CurriculumSeed.Parse(structure, taxonomy, "topic_code,q_i,tier,years_present\nTYT.A.01,1,az,1/8\n");
        var b = CurriculumSeed.Parse(structure, taxonomy, "topic_code,q_i,tier,years_present\nTYT.A.01,2,az,1/8\n");

        a.Hash.ShouldNotBe(b.Hash);
    }
}
