namespace TutorFlow.Domain.Grading;

/// <summary>Result of a simulation (R-CAL-11). It is a projection, never an official grade.</summary>
/// <param name="Outcome">What happened.</param>
/// <param name="Grade">The minimum grade with 1 decimal, only when <paramref name="Outcome"/> is <see cref="SimulationOutcome.Required"/>.</param>
public readonly record struct RequiredGradeResult(SimulationOutcome Outcome, decimal? Grade);
