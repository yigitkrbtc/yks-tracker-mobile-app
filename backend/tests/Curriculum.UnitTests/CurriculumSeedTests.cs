using Nirengi.Modules.Curriculum.Domain.Exams;
using Nirengi.Modules.Curriculum.Domain.Sources;
using Nirengi.Modules.Curriculum.Domain.Topics;
using Nirengi.Modules.Curriculum.Infrastructure.Seed;
using Shouldly;

namespace Nirengi.Curriculum.UnitTests;

/// <summary>Guards the shipped seed itself: a broken data/seed file fails here, not at deploy.</summary>
public sealed class CurriculumSeedTests
{
    private const string NoSources = "source_code,name,kind\n";

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

        Should.Throw<FormatException>(() => CurriculumSeed.Parse(structure, taxonomy, weights, NoSources)).Message.ShouldContain("TYT.B");
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

        var a = CurriculumSeed.Parse(structure, taxonomy, "topic_code,q_i,tier,years_present\nTYT.A.01,1,az,1/8\n", NoSources);
        var b = CurriculumSeed.Parse(structure, taxonomy, "topic_code,q_i,tier,years_present\nTYT.A.01,2,az,1/8\n", NoSources);

        a.Hash.ShouldNotBe(b.Hash);
    }

    [Fact]
    public void Shipped_sources_are_unique_and_include_past_papers()
    {
        Seed.Sources.Count.ShouldBeGreaterThan(20);
        Seed.Sources.Select(s => s.Code).Distinct().Count().ShouldBe(Seed.Sources.Count);
        Seed.Sources.Single(s => s.Code == "OSYM").Kind.ShouldBe(SourceKind.Official);
        Seed.Sources.ShouldAllBe(s => s.Name.Length > 0);
    }

    [Theory]
    [InlineData("source_code,name,kind\nA,Bir,publisher\nA,İki,publisher\n", "more than once")]
    [InlineData("source_code,name,kind\nA,Bir,magazine\n", "unknown kind")]
    [InlineData("source_code,name,kind\n,Bir,publisher\n", "empty or too long")]
    public void Invalid_sources_are_rejected(string sources, string message)
    {
        const string structure = """{"examYear":2027,"tests":[{"code":"T","session":"TYT","name":"T","questionCount":1}],"units":[{"code":"TYT.A","name":"A","tests":["T"]}],"scoreTypes":[]}""";
        const string taxonomy = "session,unit,topic_code,topic,subtopics,grade,description\nTYT,A,TYT.A.01,X,y,9,\n";
        const string weights = "topic_code,q_i,tier,years_present\nTYT.A.01,1,az,1/8\n";

        Should.Throw<FormatException>(() => CurriculumSeed.Parse(structure, taxonomy, weights, sources)).Message.ShouldContain(message);
    }

    [Fact]
    public void Shipped_mock_sections_add_up_to_each_test()
    {
        Seed.MockSections.Count.ShouldBe(27);
        foreach (var test in Seed.Tests)
        {
            var root = Seed.MockSections.Single(s => s.TestCode == test.Code && s.ParentCode is null);
            root.QuestionCount.ShouldBe(test.QuestionCount);
            var children = Seed.MockSections.Where(s => s.ParentCode == root.Code && s.AlternativeOf is null).ToList();
            if (children.Count > 0)
            {
                children.Sum(c => c.QuestionCount).ShouldBe(test.QuestionCount);
            }
        }

        Seed.MockSections.Single(s => s.Code == "TYT-SOS.FEL2").AlternativeOf.ShouldBe("TYT-SOS.DKAB");
        Seed.MockSections.Single(s => s.Code == "AYT-SB2.FEL2").AlternativeOf.ShouldBe("AYT-SB2.DKAB");
        Seed.MockSections.Single(s => s.Code == "TYT-MAT").SubjectCode.ShouldBeNull();
    }

    [Theory]
    [InlineData("""[{"code":"T","test":"T","name":"T","questionCount":3}]""", "exactly one root")]
    [InlineData("""[{"code":"T","test":"T","name":"T","questionCount":2},{"code":"T.A","test":"T","name":"A","questionCount":1,"parent":"T"}]""", "do not add up")]
    [InlineData("""[{"code":"T","test":"T","name":"T","questionCount":2},{"code":"T","test":"T","name":"A","questionCount":2}]""", "more than once")]
    [InlineData("""[{"code":"T","test":"X","name":"T","questionCount":2}]""", "unknown test")]
    [InlineData("""[{"code":"T","test":"T","name":"T","questionCount":2,"subject":"TYT.B"}]""", "not part of test")]
    [InlineData("""[{"code":"T","test":"T","name":"T","questionCount":2},{"code":"T.A","test":"T","name":"A","questionCount":2,"parent":"T.B"}]""", "invalid parent")]
    [InlineData("""[{"code":"T","test":"T","name":"T","questionCount":2},{"code":"T.A","test":"T","name":"A","questionCount":2,"parent":"T"},{"code":"T.B","test":"T","name":"B","questionCount":1,"parent":"T","alternativeOf":"T.A"}]""", "same-size sibling")]
    public void Invalid_mock_sections_are_rejected(string sections, string message)
    {
        var structure = $$"""{"examYear":2027,"tests":[{"code":"T","session":"TYT","name":"T","questionCount":2}],"units":[{"code":"TYT.A","name":"A","tests":["T"]}],"scoreTypes":[],"mockSections":{{sections}}}""";
        const string taxonomy = "session,unit,topic_code,topic,subtopics,grade,description\nTYT,A,TYT.A.01,X,y,9,\n";
        const string weights = "topic_code,q_i,tier,years_present\nTYT.A.01,1,az,1/8\n";

        Should.Throw<FormatException>(() => CurriculumSeed.Parse(structure, taxonomy, weights, NoSources)).Message.ShouldContain(message);
    }
}
