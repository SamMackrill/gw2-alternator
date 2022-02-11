namespace guildwars2.tools.alternator.tests;

public class VpnTest
{
    [Fact]
    public void Connections_Null_ReturnEmpty()
    {
        IEnumerable<string>? lines = null;
        var result = VpnConnectionsViewModel.ExtractConnections(lines, @"\w+-\w+-st\d+\.prod\.surfshark\.com");
        result.Should().BeEmpty();
    }

    [Fact]
    public void Connections_Empty_ReturnEmpty()
    {
        var lines = Array.Empty<string>();
        var result = VpnConnectionsViewModel.ExtractConnections(lines, @"\w+-\w+-st\d+\.prod\.surfshark\.com");
        result.Should().BeEmpty();
    }

    [Fact]
    public void Connections_Junk_ReturnEmpty()
    {
        var lines = new[]{"a", "b"};
        var result = VpnConnectionsViewModel.ExtractConnections(lines, @"\w+-\w+-st\d+\.prod\.surfshark\.com");
        result.Should().BeEmpty();
    }

    [Fact]
    public void Connections_JustName_ReturnEmpty()
    {
        var lines = new[] { "[VPN Static London-01]" };
        var result = VpnConnectionsViewModel.ExtractConnections(lines, @"\w+-\w+-st\d+\.prod\.surfshark\.com");
        result.Should().BeEmpty();
    }

    [Fact]
    public void Connections_JustPhone_ReturnEmpty()
    {
        var lines = new[] { "PhoneNumber=uk-lon-st001.prod.surfshark.com" };
        var result = VpnConnectionsViewModel.ExtractConnections(lines, @"\w+-\w+-st\d+\.prod\.surfshark\.com");
        result.Should().BeEmpty();
    }

    [Fact]
    public void Connections_OneNameAndPhone_ReturnOne()
    {
        var lines = new[]
        {
            "[VPN Static London-01]",
            "PhoneNumber=uk-lon-st001.prod.surfshark.com",
        };
        var result = VpnConnectionsViewModel.ExtractConnections(lines, @"\w+-\w+-st\d+\.prod\.surfshark\.com");
        result.Should().BeEquivalentTo(new List<string> { "VPN Static London-01" });
    }

    [Fact]
    public void Connections_OnePhoneAndOneName_ReturnEmpty()
    {
        var lines = new[]
        {
            "PhoneNumber=uk-lon-st001.prod.surfshark.com",
            "[VPN Static London-01]",
        };
        var result = VpnConnectionsViewModel.ExtractConnections(lines, @"\w+-\w+-st\d+\.prod\.surfshark\.com");
        result.Should().BeEmpty();
    }

    [Fact]
    public void Connections_TwoNamesAndTwoPhones_ReturnTwo()
    {
        var lines = new[]
        {
            "[VPN Static London-01]",
            "PhoneNumber=uk-lon-st001.prod.surfshark.com",
            "[VPN Static New York-02]",
            "PhoneNumber=us-nyc-st002.prod.surfshark.com",
        };
        var result = VpnConnectionsViewModel.ExtractConnections(lines, @"\w+-\w+-st\d+\.prod\.surfshark\.com");
        result.Should().BeEquivalentTo(new List<string>
        {
            "VPN Static London-01",
            "VPN Static New York-02",
        });
    }

    [Fact]
    public void Connections_TwoNamesAndOnePhone_ReturnOne()
    {
        var lines = new[]
        {
            "[VPN Static London-01]",
            "PhoneNumber=uk-lon-st001.prod.surfshark.com",
            "[VPN Static New York-02]",
        };
        var result = VpnConnectionsViewModel.ExtractConnections(lines, @"\w+-\w+-st\d+\.prod\.surfshark\.com");
        result.Should().BeEquivalentTo(new List<string>
        {
            "VPN Static London-01",
        });
    }


}