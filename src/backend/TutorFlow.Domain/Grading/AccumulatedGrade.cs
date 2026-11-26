namespace TutorFlow.Domain.Grading;

/// <summary>
/// Accumulated grade of a course without final grade: weighted sum of the graded cuts (D-04). It is not
/// normalized: it does not project a grade that does not exist.
/// </summary>
/// <param name="Value">Weighted sum with 2 decimals (e.g. 2.40, R-QA-05).</param>
/// <param name="EvaluatedPercent">Percentage of the grade already evaluated (e.g. 60).</param>
public readonly record struct AccumulatedGrade(decimal Value, int EvaluatedPercent);
