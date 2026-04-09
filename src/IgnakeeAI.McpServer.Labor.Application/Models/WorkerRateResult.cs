namespace IgnakeeAI.McpServer.Labor.Application.Models
{
    /// <summary>Resultado de consulta de tarifa de un trabajador.</summary>
    public record WorkerRateResult(
        bool Found,
        string? WorkerId = null,
        string? FullName = null,
        string? Specialty = null,
        decimal? HourlyRate = null,
        decimal? DailyRate = null,
        string? Currency = null,
        int? ExperienceYears = null,
        int? QualityRating = null,
        string? ProfileUrl = null,
        string? LocationAddress = null,
        string? WorkZone = null,
        string? ContactPhone = null,
        string? ContactEmail = null,
        string? AvailabilitySchedule = null,
        DateTime? UpdatedAt = null,
        string? AgencyContactEmail = null,
        string? AgencyContactPhone = null,
        string? AgencyContactAddress = null
    );
}
