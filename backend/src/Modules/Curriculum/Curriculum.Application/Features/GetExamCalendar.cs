using Nirengi.BuildingBlocks.Application.Messaging;
using Nirengi.BuildingBlocks.Domain.Results;
using Nirengi.Modules.Curriculum.Application.Abstractions;
using Nirengi.Modules.Curriculum.Domain.Calendar;

namespace Nirengi.Modules.Curriculum.Application.Features;

/// <summary>Every shipped YKS calendar, oldest year first; clients pick the student's target year.</summary>
public sealed record GetExamCalendarQuery : IQuery<ExamCalendarResponse>;

public sealed record ExamCalendarResponse(string Version, IReadOnlyList<ExamCalendarDto> Calendars);

/// <param name="Note">Shown under the countdown, e.g. why the dates are estimates.</param>
public sealed record ExamCalendarDto(int ExamYear, string SourceUrl, string? Note, IReadOnlyList<ExamCalendarEventDto> Events);

public sealed record ExamCalendarEventDto(string Code, string Name, DateTimeOffset StartsAt, DateTimeOffset? EndsAt, CalendarEventStatus Status);

internal sealed class GetExamCalendarHandler(IExamCalendarSource source) : IQueryHandler<GetExamCalendarQuery, ExamCalendarResponse>
{
    public Task<Result<ExamCalendarResponse>> Handle(GetExamCalendarQuery query, CancellationToken cancellationToken) =>
        Task.FromResult<Result<ExamCalendarResponse>>(new ExamCalendarResponse(
            source.Version,
            [.. source.Calendars.OrderBy(c => c.ExamYear).Select(c => new ExamCalendarDto(
                c.ExamYear,
                c.SourceUrl,
                c.Note,
                [.. c.Events.OrderBy(e => e.StartsAt).Select(e => new ExamCalendarEventDto(e.Code, e.Name, e.StartsAt, e.EndsAt, e.Status))]))]));
}
