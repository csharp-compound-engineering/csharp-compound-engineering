using CompoundDocs.GitSync;

namespace CompoundDocs.Tests.GitSync;

public sealed class ChangedFileTests
{
    [Theory]
    [InlineData(ChangeType.Added)]
    [InlineData(ChangeType.Modified)]
    [InlineData(ChangeType.Deleted)]
    public void ChangedFile_CanBeCreatedWithAllChangeTypes(ChangeType changeType)
    {
        var file = new ChangedFile
        {
            Path = "docs/test.md",
            ChangeType = changeType
        };

        file.Path.ShouldBe("docs/test.md");
        file.ChangeType.ShouldBe(changeType);
    }

    [Fact]
    public void ChangeType_HasThreeValues()
    {
        var values = Enum.GetValues<ChangeType>();
        values.Length.ShouldBe(3);
    }
}
