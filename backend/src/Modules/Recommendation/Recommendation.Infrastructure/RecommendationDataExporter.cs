using Nirengi.BuildingBlocks.Application.Export;
using Nirengi.Modules.Recommendation.Application.Abstractions;

namespace Nirengi.Modules.Recommendation.Infrastructure;

/// <summary>Items the student dismissed (ADR-0018). Plans themselves are derived from other modules' data and left out.</summary>
internal sealed class RecommendationDataExporter(IRecommendationStore store) : IPersonalDataExporter
{
    public int Order => 40;

    public async Task<IReadOnlyList<ExportFile>> Export(Guid userId, CancellationToken cancellationToken)
    {
        var feedback = await store.GetAllFeedback(userId, cancellationToken);
        var csv = new CsvTable("hafta", "oneri_anahtari", "neden", "tarih");
        foreach (var f in feedback.OrderBy(f => f.At))
        {
            csv.Add(f.WeekStart, f.ItemKey, f.Reason, f.At);
        }

        return [csv.ToFile("oneri_geri_bildirimi.csv")];
    }
}
