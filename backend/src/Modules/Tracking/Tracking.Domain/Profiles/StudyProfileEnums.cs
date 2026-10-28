namespace Nirengi.Modules.Tracking.Domain.Profiles;

/// <summary>ÖSYM score types; the codes match Curriculum's score type codes (an integration test checks).</summary>
public enum ScoreType
{
    SAY = 0,
    EA = 1,
    SOZ = 2,
    DIL = 3,
}

public enum Grade
{
    Ninth = 9,
    Tenth = 10,
    Eleventh = 11,
    Twelfth = 12,
    Graduate = 13,
}

/// <summary>YDT languages; asked only of DIL students.</summary>
public enum YdtLanguage
{
    English = 0,
    German = 1,
    French = 2,
    Russian = 3,
    Arabic = 4,
}
