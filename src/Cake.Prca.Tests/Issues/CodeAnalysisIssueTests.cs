namespace Cake.Prca.Tests.Issues
{
    using Prca.Issues;
    using Shouldly;
    using Xunit;

    public sealed class TheCodeAnalysisIssueCtor
    {
        [Fact]
        public void Should_Throw_If_File_Path_Is_Null()
        {
            // Given / When
            var result = Record.Exception(() => new CodeAnalysisIssue(null, 100, "Foo", 1, "Bar"));

            // Then
            result.IsArgumentNullException("filePath");
        }

        [Fact]
        public void Should_Throw_If_File_Path_Is_Empty()
        {
            // Given / When
            var result = Record.Exception(() => new CodeAnalysisIssue(string.Empty, 100, "Foo", 1, "Bar"));

            // Then
            result.IsArgumentOutOfRangeException("filePath");
        }

        [Fact]
        public void Should_Throw_If_File_Path_Is_WhiteSpace()
        {
            // Given / When
            var result = Record.Exception(() => new CodeAnalysisIssue(" ", 100, "Foo", 1, "Bar"));

            // Then
            result.IsArgumentOutOfRangeException("filePath");
        }

        [Theory]
        [InlineData(@"foo<bar")]
        public void Should_Throw_If_File_Path_Is_Invalid(string filePath)
        {
            // Given / When
            var result = Record.Exception(() => new CodeAnalysisIssue(filePath, 100, "Foo", 1, "Bar"));

            // Then
            result.IsArgumentException("filePath");
        }

        [Theory]
        [InlineData(@"c:\src\foo.cs")]
        [InlineData(@"/foo")]
        [InlineData(@"\foo")]
        public void Should_Throw_If_File_Path_Is_Absolute(string filePath)
        {
            // Given / When
            var result = Record.Exception(() => new CodeAnalysisIssue(filePath, 100, "Foo", 1, "Bar"));

            // Then
            result.IsArgumentOutOfRangeException("filePath");
        }

        [Fact]
        public void Should_Throw_If_Line_Is_Negative()
        {
            // Given / When
            var result = Record.Exception(() => new CodeAnalysisIssue(@"\src\foo.cs", -1, "Foo", 1, "Bar"));

            // Then
            result.IsArgumentOutOfRangeException("line");
        }

        [Fact]
        public void Should_Throw_If_Line_Is_Zero()
        {
            // Given / When
            var result = Record.Exception(() => new CodeAnalysisIssue(@"\src\foo.cs", 0, "Foo", 1, "Bar"));

            // Then
            result.IsArgumentOutOfRangeException("line");
        }

        [Fact]
        public void Should_Throw_If_Message_Is_Null()
        {
            // Given / When
            var result = Record.Exception(() => new CodeAnalysisIssue(@"\src\foo.cs", 100, null, 1, "Bar"));

            // Then
            result.IsArgumentNullException("message");
        }

        [Fact]
        public void Should_Throw_If_Message_Is_Empty()
        {
            // Given / When
            var result = Record.Exception(() => new CodeAnalysisIssue(@"\src\foo.cs", 100, string.Empty, 1, "Bar"));

            // Then
            result.IsArgumentOutOfRangeException("message");
        }

        [Fact]
        public void Should_Throw_If_Message_Is_WhiteSpace()
        {
            // Given / When
            var result = Record.Exception(() => new CodeAnalysisIssue(@"\src\foo.cs", 100, " ", 1, "Bar"));

            // Then
            result.IsArgumentOutOfRangeException("message");
        }

        [Fact]
        public void Should_Throw_If_Rule_Is_Null()
        {
            // Given / When
            var result = Record.Exception(() => new CodeAnalysisIssue(@"\src\foo.cs", 100, "foo", 1, null));

            // Then
            result.IsArgumentNullException("rule");
        }

        [Theory]
        [InlineData(@"foo", @"foo")]
        [InlineData(@"foo\bar", @"foo/bar")]
        [InlineData(@"foo/bar", @"foo/bar")]
        [InlineData(@"foo\bar\", @"foo/bar")]
        [InlineData(@"foo/bar/", @"foo/bar")]
        [InlineData(@".\foo", @"foo")]
        [InlineData(@"./foo", @"foo")]
        [InlineData(@"foo\..\bar", @"foo/../bar")]
        [InlineData(@"foo/../bar", @"foo/../bar")]
        public void Should_Set_File_Path(string filePath, string expectedFilePath)
        {
            // Given / When
            var issue = new CodeAnalysisIssue(filePath, 100, "Foo", 1, "Bar");

            // Then
            issue.AffectedFileRelativePath.ToString().ShouldBe(expectedFilePath);
            issue.AffectedFileRelativePath.IsRelative.ShouldBe(true, "File path was not set as relative.");
        }

        [Theory]
        [InlineData(null)]
        [InlineData(1)]
        [InlineData(int.MaxValue)]
        public void Should_Set_Line(int? line)
        {
            // Given / When
            var issue = new CodeAnalysisIssue(@"foo.cs", line, "Foo", 1, "Bar");

            // Then
            issue.Line.ShouldBe(line);
        }

        [Theory]
        [InlineData("message")]
        public void Should_Set_Message(string message)
        {
            // Given / When
            var issue = new CodeAnalysisIssue(@"foo.cs", 100, message, 1, "Bar");

            // Then
            issue.Message.ShouldBe(message);
        }

        [Theory]
        [InlineData("rule")]
        public void Should_Set_Rule(string rule)
        {
            // Given / When
            var issue = new CodeAnalysisIssue(@"foo.cs", 100, "foo", 1, rule);

            // Then
            issue.Rule.ShouldBe(rule);
        }
    }
}
