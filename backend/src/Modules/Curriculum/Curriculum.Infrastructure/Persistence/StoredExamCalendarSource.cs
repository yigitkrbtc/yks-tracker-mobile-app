using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Nirengi.Modules.Curriculum.Application.Abstractions;
using Nirengi.Modules.Curriculum.Domain.Calendar;
using Nirengi.Modules.Curriculum.Domain.Versions;

namespace Nirengi.Modules.Curriculum.Infrastructure.Persistence;

internal sealed class StoredExamCalendarSource(CurriculumDbContext db) : IExamCalendarSource
{
    public async Task<ExamCalendarSet> Get(CancellationToken cancellationToken)
    {
        var rows = await db.Calendars.AsNoTracking().OrderBy(c => c.ExamYear).ToListAsync(cancellationToken);
        var calendars = rows.Select(r => CurriculumJson.Deserialize<ExamCalendar>(r.Data)).ToList();
        var version = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(CurriculumJson.Serialize(calendars))))[..32];
        return new ExamCalendarSet(version, calendars);
    }
}
