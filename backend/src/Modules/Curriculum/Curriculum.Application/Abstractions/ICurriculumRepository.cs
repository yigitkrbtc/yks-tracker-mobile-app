using Nirengi.Modules.Curriculum.Domain;
using Nirengi.Modules.Curriculum.Domain.Exams;
using Nirengi.Modules.Curriculum.Domain.Sources;
using Nirengi.Modules.Curriculum.Domain.Topics;

namespace Nirengi.Modules.Curriculum.Application.Abstractions;

/// <summary>Read side of the curriculum; the content itself is written only by the seed import.</summary>
public interface ICurriculumRepository
{
    Task<CurriculumRelease?> GetLatestRelease(CancellationToken cancellationToken);

    Task<IReadOnlyList<ScoreType>> GetScoreTypes(CancellationToken cancellationToken);

    Task<IReadOnlyList<ExamTest>> GetTests(CancellationToken cancellationToken);

    Task<IReadOnlyList<Subject>> GetSubjects(CancellationToken cancellationToken);

    Task<IReadOnlyList<Topic>> GetActiveTopics(CancellationToken cancellationToken);

    Task<IReadOnlyList<Source>> GetActiveSources(CancellationToken cancellationToken);

    Task<IReadOnlyList<TopicWeight>> GetWeights(int examYear, CancellationToken cancellationToken);
}
