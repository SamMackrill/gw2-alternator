using System.Linq;

namespace guildwars2.tools.alternator.tests;

public class LauncherTest
{
    [Fact]
    public void Test1()
    {
        var result = 2 > 1;
        result.Should().BeTrue();
    }

    private record tt(int a, int b);

    //[Fact]
    //public void Tests()
    //{
    //    var list = new List<tt>
    //    {
    //        new(1, 0),
    //        new(1, 1),
    //        new(1, 0),
    //        new(2, 2),
    //    };
    //    var p1 = list.OrderBy(t => t.a).ToList();
    //    var p2 = p1.OrderBy(t => t.b).Take(3).ToList();
    //}

}