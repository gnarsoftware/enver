using System.Text;

namespace Enver.Tests;

public class EnvValueReaderTests
{
    [Test]
    public void CustomVisitorCanReadValuesViaSpan()
    {
        var captured = new List<string>();
        var visitor = new SpanCapturingParser(captured);
        visitor.Run(
            """
            KEY1=hello
            KEY2="world"
            """
        );
        Assert.That(captured, Is.EqualTo(["KEY1=hello", "KEY2=world"]));
    }

    [Test]
    public void MultiSegmentValueIsAssembledIntoSpanFromEnv()
    {
        Environment.SetEnvironmentVariable("ENVER_MULTI_TEST", "MIDDLE");
        try
        {
            var captured = new List<string>();
            var visitor = new SpanCapturingParser(captured);
            visitor.Run("KEY=before-${ENVER_MULTI_TEST}-after");
            Assert.That(captured, Is.EqualTo(["KEY=before-MIDDLE-after"]));
        }
        finally
        {
            Environment.SetEnvironmentVariable("ENVER_MULTI_TEST", null);
        }
    }

    [Test]
    public void MultiSegmentValueIsAssembledIntoSpanFromSeed()
    {
        var seed = new Dictionary<string, string> { ["ENVER_MULTI_TEST"] = "MIDDLE" };
        var captured = new List<string>();
        var visitor = new SpanCapturingParser(captured, seed);
        visitor.Run("KEY=before-${ENVER_MULTI_TEST}-after");
        Assert.That(captured, Is.EqualTo(["KEY=before-MIDDLE-after"]));
    }

    [Test]
    public void CustomVisitorCanShortCircuitByReturningFalse()
    {
        var captured = new List<string>();
        var visitor = new ShortCircuitParser("KEY2", captured);
        visitor.Run(
            """
            KEY1=a
            KEY2=b
            KEY3=c
            """
        );
        Assert.That(captured, Is.EqualTo(["KEY1", "KEY2"]));
    }

    [Test]
    public void EmptyValueIsExposedAsEmptySpanAndEmptyString()
    {
        var captured = new List<(string key, int spanLength, string str)>();
        var visitor = new ShapeCapturingParser(captured);
        visitor.Run("KEY=");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(captured, Has.Count.EqualTo(1));
            var (key, spanLength, str) = captured[0];
            Assert.That(key, Is.EqualTo("KEY"));
            Assert.That(spanLength, Is.EqualTo(0));
            Assert.That(str, Is.EqualTo(""));
        }
    }

    private sealed class SpanCapturingParser(
        List<string> captured,
        Dictionary<string, string>? seed = null
    ) : EnvParser
    {
        public override void SeedScope(EnvParseView scope)
        {
            if (seed is not null)
            {
                foreach (var (key, value) in seed)
                {
                    scope.Seed(key, value);
                }
            }
        }

        public void Run(string input) => Parse(input);

        protected override bool OnNext(ReadOnlySpan<byte> key, ref EnvValueReader value)
        {
            captured.Add($"{Encoding.UTF8.GetString(key)}={Encoding.UTF8.GetString(value.Span)}");
            return true;
        }
    }

    private sealed class ShortCircuitParser(string stopAt, List<string> captured) : EnvParser
    {
        private readonly byte[] _stopAtBytes = Encoding.UTF8.GetBytes(stopAt);

        public void Run(string input) => Parse(input);

        protected override bool OnNext(ReadOnlySpan<byte> key, ref EnvValueReader value)
        {
            captured.Add(Encoding.UTF8.GetString(key));
            return !key.SequenceEqual(_stopAtBytes);
        }
    }

    private sealed class ShapeCapturingParser(List<(string, int, string)> captured) : EnvParser
    {
        public void Run(string input) => Parse(input);

        protected override bool OnNext(ReadOnlySpan<byte> key, ref EnvValueReader value)
        {
            captured.Add((Encoding.UTF8.GetString(key), value.Span.Length, value.AsString()));
            return true;
        }
    }
}
