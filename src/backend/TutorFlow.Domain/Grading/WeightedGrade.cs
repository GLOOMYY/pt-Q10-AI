namespace TutorFlow.Domain.Grading;

/// <summary>A final grade (already rounded to 1 decimal) with the credits of its course (R-CAL-04).</summary>
/// <param name="Grade">The rounded final grade; 0.0 for a course lost by absences (R-CAL-06).</param>
/// <param name="Credits">Credits of the course; must be greater than zero.</param>
public readonly record struct WeightedGrade(decimal Grade, int Credits);
