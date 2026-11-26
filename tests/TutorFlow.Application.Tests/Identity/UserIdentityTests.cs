using TutorFlow.Application.Assistant;
using TutorFlow.Application.Identity;
using Xunit;

namespace TutorFlow.Application.Tests.Identity;

/// <summary>Identity format and coherence without calling the API: R-SEG-04, API-01, API-05, CL-12.</summary>
public class UserIdentityTests
{
    // ESC-ENT-18, API-05: trim + upper case before validating.
    [Fact]
    public void Student_id_is_normalized()
    {
        Assert.True(UserIdentity.TryCreate(UserRole.Student, " est-001 ", out var identity));
        Assert.Equal("EST-001", identity.Id);
        Assert.Equal("EST-001", identity.StudentId!.Value);
        Assert.Null(identity.CoordinatorId);
    }

    [Fact]
    public void Coordinator_id_is_normalized()
    {
        Assert.True(UserIdentity.TryCreate(UserRole.Coordinator, "coord-02", out var identity));
        Assert.Equal("COORD-02", identity.Id);
        Assert.Null(identity.StudentId);
    }

    // ESC-ENT-15, ESC-ENT-16, ESC-ENT-17, CL-12, API-01: strict ASCII pattern, nothing else reaches the API.
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("EST-%E0%A4%A")]
    [InlineData("../programas")]
    [InlineData("EST-001/notas")]
    [InlineData("EST-0001")]
    [InlineData("EST-01")]
    [InlineData("EST-0\n01")]
    [InlineData("EST-١٢٣")]
    public void Invalid_student_id_is_rejected(string? id)
    {
        Assert.False(UserIdentity.TryCreate(UserRole.Student, id, out _));
    }

    // ESC-ENT-14, CL-12: the id must correspond to the declared role.
    [Fact]
    public void Role_and_id_must_be_coherent()
    {
        Assert.False(UserIdentity.TryCreate(UserRole.Student, "COORD-01", out _));
        Assert.False(UserIdentity.TryCreate(UserRole.Coordinator, "EST-001", out _));
    }
}
