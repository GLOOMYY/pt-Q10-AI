namespace TutorFlow.Domain.Evaluation;

/// <summary>The grade row of a course, as the academic API returns it (API-10: <see langword="null"/> is "no data").</summary>
/// <param name="Credits">Credits of the course.</param>
/// <param name="Cut1">Cut 1 (30 %) or <see langword="null"/> if not graded.</param>
/// <param name="Cut2">Cut 2 (30 %) or <see langword="null"/> if not graded.</param>
/// <param name="Cut3">Cut 3 (40 %) or <see langword="null"/> if not graded.</param>
public sealed record CourseGradesInput(int? Credits, decimal? Cut1, decimal? Cut2, decimal? Cut3);

/// <summary>The attendance row of a course, as the academic API returns it.</summary>
/// <param name="ScheduledClasses">Scheduled classes of the period.</param>
/// <param name="Absences">Registered absences or <see langword="null"/> when unknown (R-ASI-03).</param>
public sealed record CourseAttendanceInput(int? ScheduledClasses, int? Absences);

/// <summary>
/// One course of a student with its grade and attendance rows already joined by course code (API-09). A missing row is
/// <see langword="null"/> and makes the course inconsistent (R-CAL-12).
/// </summary>
/// <param name="Code">Course code (e.g. <c>EST-103</c>; never an identifier of a person, CL-08).</param>
/// <param name="Name">Course name.</param>
/// <param name="Grades">The grade row, or <see langword="null"/> if the course has none.</param>
/// <param name="Attendance">The attendance row, or <see langword="null"/> if the course has none.</param>
public sealed record CourseInput(string Code, string Name, CourseGradesInput? Grades, CourseAttendanceInput? Attendance);
