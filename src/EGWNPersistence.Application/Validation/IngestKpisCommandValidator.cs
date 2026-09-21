using FluentValidation;

namespace EGWNPersistence.Application.Validation;

public sealed class IngestKpisCommandValidator : AbstractValidator<Commands.IngestKpisCommand>
{
    public IngestKpisCommandValidator()
    {
        RuleFor(x => x.CommunityId)
            .NotEmpty()
            .WithMessage("CommunityId is required.")
            .MaximumLength(100)
            .WithMessage("CommunityId must not exceed 100 characters.");

        RuleFor(x => x.Entries)
            .NotEmpty()
            .WithMessage("At least one KPI entry must be provided.");

        RuleForEach(x => x.Entries).ChildRules(e =>
        {
            e.RuleFor(v => v.Timestamp)
                .NotEqual(default(DateTimeOffset))
                .WithMessage("KPI Timestamp must be set.");
        });

        RuleFor(x => x.Entries)
            .Must(entries => entries
                .Select(e => e.Timestamp.ToUniversalTime())
                .Distinct()
                .Count() == entries.Count)
            .When(x => x.Entries is { Count: > 0 })
            .WithMessage("KPI entries must have unique timestamps.");
    }
}
