using FluentAssertions;
using Xunit;

namespace alternator.tests
{
    public class LauncherTest
    {
        [Fact]
        public void Test1()
        {
            var result = 2 > 1;
            result.Should().BeTrue();
        }
    }
}