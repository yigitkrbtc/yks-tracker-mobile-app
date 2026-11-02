namespace Nirengi.Modules.Curriculum.Domain.Versions;

/// <summary>
/// Gives new topics their permanent code when a draft is saved (ADR-0019), so later operations and the admin can
/// refer to it. A code taken meanwhile by the live curriculum (e.g. a seed import published first) is replaced by
/// the next free one, and the draft's later references follow it.
/// </summary>
public static class DraftCodes
{
    public static IReadOnlyList<CurriculumOperation> Assign(IReadOnlyList<CurriculumOperation> operations, IReadOnlySet<string> liveCodes)
    {
        var used = new HashSet<string>(liveCodes, StringComparer.Ordinal);

        // Codes the draft already holds stay reserved, so a replacement never lands on a later new topic.
        var kept = new HashSet<string>(StringComparer.Ordinal);
        foreach (var code in operations.OfType<AddTopic>().Select(a => a.Code?.Trim()).Where(c => !string.IsNullOrEmpty(c) && !liveCodes.Contains(c)))
        {
            used.Add(code!);
        }

        var renamed = new Dictionary<string, string>(StringComparer.Ordinal);
        var result = new List<CurriculumOperation>(operations.Count);
        foreach (var op in operations)
        {
            if (op is AddTopic { SubjectCode: not null } add)
            {
                var code = add.Code?.Trim();
                if (string.IsNullOrEmpty(code) || liveCodes.Contains(code) || !kept.Add(code))
                {
                    var next = DraftApplier.NextCode(add.SubjectCode, used);
                    if (!string.IsNullOrEmpty(code))
                    {
                        renamed[code] = next;
                    }

                    code = next;
                }

                used.Add(code);
                kept.Add(code);
                result.Add(add with { Code = code });
                continue;
            }

            result.Add(renamed.Count == 0 ? op : Rename(op, renamed));
        }

        return result;
    }

    private static CurriculumOperation Rename(CurriculumOperation op, Dictionary<string, string> renamed)
    {
        string R(string code) => code is not null && renamed.TryGetValue(code, out var to) ? to : code!;
        return op switch
        {
            UpdateTopic u => u with { Code = R(u.Code) },
            RetireTopic r => r with { Code = R(r.Code) },
            ReactivateTopic r => r with { Code = R(r.Code) },
            MergeTopics m when m.FromCodes is not null => m with { FromCodes = [.. m.FromCodes.Select(R)], ToCode = R(m.ToCode) },
            SplitTopic s when s.ToCodes is not null => s with { FromCode = R(s.FromCode), ToCodes = [.. s.ToCodes.Select(R)] },
            _ => op,
        };
    }
}
