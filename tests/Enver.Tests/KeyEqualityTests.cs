using System.Text;
using Enver.Parsing;

namespace Enver.Tests;

public class KeyEqualityTests
{
    [TestCase("FOO", "FOO", true)]
    [TestCase("FOO", "BAR", false)]
    [TestCase("FOO", "FOOX", false)]
    [TestCase("", "", true)]
    [TestCase("FOO", "foo", false)] // case-sensitive: distinct
    [TestCase("FOO_BAR", "foo_bar", false)]
    public void CaseSensitiveCompare(string a, string b, bool expected)
    {
        Assert.That(
            KeyEquality.Equal(
                Encoding.UTF8.GetBytes(a),
                Encoding.UTF8.GetBytes(b),
                caseInsensitive: false
            ),
            Is.EqualTo(expected)
        );
    }

    [TestCase("FOO", "FOO", true)]
    [TestCase("FOO", "BAR", false)]
    [TestCase("FOO", "foo", true)] // case-insensitive: equivalent
    [TestCase("Foo", "fOO", true)]
    [TestCase("FOO_BAR", "foo_bar", true)]
    [TestCase("FOO", "FOOX", false)]
    [TestCase("", "", true)]
    public void CaseInsensitiveCompare(string a, string b, bool expected)
    {
        Assert.That(
            KeyEquality.Equal(
                Encoding.UTF8.GetBytes(a),
                Encoding.UTF8.GetBytes(b),
                caseInsensitive: true
            ),
            Is.EqualTo(expected)
        );
    }

    [Test]
    public void CaseInsensitiveFoldDoesNotTouchNonLetters()
    {
        using (Assert.EnterMultipleScope())
        {
            // Digits / underscores / non-ASCII bytes must compare literally even under fold.
            Assert.That(
                KeyEquality.Equal("FOO_123"u8, "foo_123"u8, caseInsensitive: true),
                Is.True
            );
            Assert.That(
                KeyEquality.Equal("FOO_123"u8, "foo_124"u8, caseInsensitive: true),
                Is.False
            );
        }
    }

    [Test]
    public void DefaultOverloadMatchesHostOsBehavior()
    {
        bool sameUnderHostOs = KeyEquality.Equal("FOO"u8, "foo"u8);
        Assert.That(sameUnderHostOs, Is.EqualTo(OperatingSystem.IsWindows()));
    }
}
