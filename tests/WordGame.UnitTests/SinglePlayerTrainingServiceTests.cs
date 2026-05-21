using Microsoft.Extensions.Options;
using WordGame.Application.DTOs;
using WordGame.Application.Interfaces;
using WordGame.Application.Options;
using WordGame.Application.Services;
using WordGame.Domain.Enums;
using Xunit;

namespace WordGame.UnitTests;

public sealed class SinglePlayerTrainingServiceTests
{
    [Fact]
    public async Task StartSession_starts_with_fourteen_questions()
    {
        var service = CreateService();

        var session = await service.StartSessionAsync(new StartTrainingRequest(["romanian"]));

        Assert.Equal(SinglePlayerSessionStatus.InProgress, session.Status);
        Assert.NotNull(session.CurrentQuestion);
        Assert.Equal(1, session.CurrentQuestion!.QuestionIndex);
        Assert.Equal(14, session.CurrentQuestion.TotalQuestions);
    }

    [Fact]
    public async Task Correct_answer_awards_score()
    {
        var service = CreateService();
        var session = await service.StartSessionAsync(new StartTrainingRequest(["romanian"]));
        var question = session.CurrentQuestion!;

        var feedback = await service.SubmitAnswerAsync(session.SessionId, question.QuestionId, "scoala");

        Assert.True(feedback.IsCorrect);
        Assert.Equal(600, feedback.ScoreAwarded);
        Assert.Equal(feedback.ScoreAwarded, feedback.TotalScore);
    }

    [Fact]
    public async Task Wrong_answer_subtracts_word_value()
    {
        var service = CreateService();
        var session = await service.StartSessionAsync(new StartTrainingRequest(["romanian"]));
        var question = session.CurrentQuestion!;

        var feedback = await service.SubmitAnswerAsync(session.SessionId, question.QuestionId, "gresit");

        Assert.False(feedback.IsCorrect);
        Assert.Equal(-600, feedback.ScoreAwarded);
    }

    [Fact]
    public async Task Requested_letter_reveals_one_position_and_reduces_potential_score()
    {
        var service = CreateService();
        var session = await service.StartSessionAsync(new StartTrainingRequest(["romanian"]));

        session = service.RequestLetter(session.SessionId);

        Assert.Single(session.CurrentQuestion!.RevealedLetters);
        Assert.Equal(500, session.CurrentQuestion.PotentialScore);
    }

    [Fact]
    public async Task Duplicate_answer_for_same_question_is_rejected()
    {
        var service = CreateService();
        var session = await service.StartSessionAsync(new StartTrainingRequest(["romanian"]));
        var question = session.CurrentQuestion!;

        await service.SubmitAnswerAsync(session.SessionId, question.QuestionId, "gresit");
        var duplicate = await service.SubmitAnswerAsync(session.SessionId, question.QuestionId, "scoala");

        Assert.False(duplicate.Accepted);
    }

    [Fact]
    public async Task Final_summary_counts_score_and_answers()
    {
        var service = CreateService();
        var session = await service.StartSessionAsync(new StartTrainingRequest(["romanian"]));

        while (session.Status != SinglePlayerSessionStatus.Finished)
        {
            var question = session.CurrentQuestion!;
            await service.SubmitAnswerAsync(session.SessionId, question.QuestionId, question.QuestionIndex == 1 ? "scoala" : "gresit");
            session = service.NextQuestion(session.SessionId);
        }

        Assert.NotNull(session.Summary);
        Assert.Equal(14, session.Summary!.TotalQuestions);
        Assert.Equal(1, session.Summary.CorrectCount);
        Assert.Equal(13, session.Summary.WrongCount);
        Assert.True(session.Summary.TotalScore < 0);
    }

    private static SinglePlayerTrainingService CreateService()
    {
        var options = Options.Create(new GameOptions { QuestionsPerGame = 14 });
        var normalizer = new AnswerNormalizer();
        var questions = Enumerable.Range(0, 14)
            .Select(index => new ActiveQuestionState
            {
                QuestionId = Guid.NewGuid(),
                Answer = index == 0 ? "scoala" : $"cuvant{index}",
                NormalizedAnswer = normalizer.Normalize(index == 0 ? "scoala" : $"cuvant{index}"),
                Definition = $"Definitie {index}",
                Subject = "Romanian",
                SubjectSlug = "romanian",
                Category = "Vocabular",
                Difficulty = Difficulty.Easy,
                TimeLimitSeconds = 25
            })
            .ToList();

        return new SinglePlayerTrainingService(
            new StaticQuestionSelector(questions),
            normalizer,
            options);
    }

    private sealed class StaticQuestionSelector(IReadOnlyList<ActiveQuestionState> questions) : IQuestionSelector
    {
        public Task<IReadOnlyList<ActiveQuestionState>> SelectQuestionsAsync(int count, IReadOnlyCollection<string> subjectSlugs, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ActiveQuestionState>>(questions.Where(x => subjectSlugs.Contains(x.SubjectSlug)).Take(count).ToList());
    }
}
