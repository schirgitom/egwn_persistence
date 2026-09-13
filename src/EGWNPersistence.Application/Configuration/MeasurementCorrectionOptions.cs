namespace EGWNPersistence.Application.Configuration;

public sealed class MeasurementCorrectionOptions
{
    public const string SectionName = "MeasurementCorrection";

    public int CorrectionWindowDays { get; init; } = 21;

    public TimeSpan ToTimeSpan()
    {
        if (CorrectionWindowDays <= 0)
            return TimeSpan.FromDays(21);

        return TimeSpan.FromDays(CorrectionWindowDays);
    }
}
