using FluentValidation;

namespace EGWNPersistence.Application.Validation;

public sealed class IngestMeasurementsCommandValidator : AbstractValidator<Commands.IngestMeasurementsCommand>
{
    public IngestMeasurementsCommandValidator()
    {
        RuleFor(x => x.MeterPointNumber)
            .NotEmpty()
            .WithMessage("MeterPointNumber is required.")
            .MaximumLength(100)
            .WithMessage("MeterPointNumber must not exceed 100 characters.");

        RuleFor(x => x.Measurements)
            .NotEmpty()
            .WithMessage("At least one measurement must be provided.");

        RuleForEach(x => x.Measurements).ChildRules(m =>
        {
            m.RuleFor(e => e.Timestamp)
                .NotEqual(default(DateTimeOffset))
                .WithMessage("Measurement Timestamp must be set.");

            m.RuleFor(e => e.TotalConsumption)
                .GreaterThanOrEqualTo(0)
                .WithMessage("TotalConsumption must be non-negative.");

            m.RuleFor(e => e.GridShare)
                .GreaterThanOrEqualTo(0)
                .WithMessage("GridShare must be non-negative.");

            m.RuleFor(e => e.CommunityShare)
                .GreaterThanOrEqualTo(0)
                .WithMessage("CommunityShare must be non-negative.");

            m.RuleFor(e => e.GridSharePercent)
                .InclusiveBetween(0, 1)
                .WithMessage("GridSharePercent must be between 0 and 1.");

            m.RuleFor(e => e.CommunitySharePercent)
                .InclusiveBetween(0, 1)
                .WithMessage("CommunitySharePercent must be between 0 and 1.");

            m.RuleFor(e => e.Source)
                .NotEmpty()
                .WithMessage("Source is required.")
                .MaximumLength(50)
                .WithMessage("Source must not exceed 50 characters.");
        });
    }
}
