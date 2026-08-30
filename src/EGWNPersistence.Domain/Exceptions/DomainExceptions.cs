namespace EGWNPersistence.Domain.Exceptions;

public class MeterPointNotFoundException(string meterPointNumber)
    : Exception($"MeterPoint with number '{meterPointNumber}' was not found.");

public class DuplicateMeasurementException(Guid meterPointId, DateTimeOffset timestamp)
    : Exception($"Measurement for MeterPoint {meterPointId} at {timestamp:O} already exists.");
