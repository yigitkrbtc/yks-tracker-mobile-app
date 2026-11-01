using Microsoft.EntityFrameworkCore;
using Nirengi.BuildingBlocks.Application.Export;

namespace Nirengi.Modules.Identity.Infrastructure.Persistence;

/// <summary>The account and consent history (ADR-0018). Secrets (password hash, TOTP, tokens) never leave the module.</summary>
internal sealed class IdentityDataExporter(IdentityDbContext db) : IPersonalDataExporter
{
    public int Order => 10;

    public async Task<IReadOnlyList<ExportFile>> Export(Guid userId, CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId, cancellationToken);
        var profile = new CsvTable("ad", "soyad", "e_posta", "dogum_yili", "rol", "durum", "pazarlama_izni", "kayit", "e_posta_dogrulama", "iki_adimli_dogrulama");
        if (user is not null)
        {
            profile.Add(user.FirstName, user.LastName, user.Email, user.BirthYear, user.Role, user.Status, user.MarketingConsent, user.CreatedAt, user.EmailVerifiedAt, user.TotpEnabledAt is not null);
        }

        var consents = await db.ConsentRecords.AsNoTracking()
            .Where(c => c.UserId == userId)
            .OrderBy(c => c.RecordedAt)
            .ToListAsync(cancellationToken);
        var consentCsv = new CsvTable("tur", "metin_surumu", "onay", "tarih");
        foreach (var c in consents)
        {
            consentCsv.Add(c.Kind, c.Version, c.Granted, c.RecordedAt);
        }

        return [profile.ToFile("profil.csv"), consentCsv.ToFile("izinler.csv")];
    }
}
