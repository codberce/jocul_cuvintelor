using FluentValidation;
using WordGame.Application.DTOs;
using WordGame.Application.Options;
using Microsoft.Extensions.Options;
using WordGame.Application.Subjects;

namespace WordGame.Application.Validators;

public sealed class JoinRoomRequestValidator : AbstractValidator<JoinRoomRequest>
{
    public JoinRoomRequestValidator(IOptions<GameOptions> options)
    {
        RuleFor(x => x.RoomCode)
            .NotEmpty()
            .Matches("^[0-9]{6}$")
            .WithMessage("Codul camerei trebuie sa contina 6 cifre.");

        RuleFor(x => x.DisplayName)
            .NotEmpty()
            .MaximumLength(options.Value.MaxPlayerNameLength)
            .Matches("^[\\p{L}\\p{N} ._'-]+$")
            .WithMessage("Numele poate contine litere, cifre, spatii si semne simple.");
    }
}

public sealed class SubmitAnswerRequestValidator : AbstractValidator<SubmitAnswerRequest>
{
    public SubmitAnswerRequestValidator()
    {
        RuleFor(x => x.RoomCode).NotEmpty().Matches("^[0-9]{6}$");
        RuleFor(x => x.GuestToken).NotEmpty().MaximumLength(2048);
        RuleFor(x => x.QuestionId).NotEmpty();
        RuleFor(x => x.Answer).NotNull().MaximumLength(128);
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128);
    }
}

public sealed class CreateRoomRequestValidator : AbstractValidator<CreateRoomRequest>
{
    public CreateRoomRequestValidator(IOptions<GameOptions> options)
    {
        RuleFor(x => x.MaxPlayers)
            .Must(value => value is null or > 0)
            .LessThanOrEqualTo(options.Value.MaxPlayersPerRoom)
            .When(x => x.MaxPlayers.HasValue);

        RuleFor(x => x.SubjectSlugs)
            .Must(HasAtLeastOneKnownSubject)
            .WithMessage("Selecteaza cel putin o materie.");
    }

    private static bool HasAtLeastOneKnownSubject(IReadOnlyList<string>? subjectSlugs)
    {
        return SubjectCatalog.NormalizeSelectedSubjects(subjectSlugs).Count > 0;
    }
}
