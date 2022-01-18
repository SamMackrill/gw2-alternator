namespace guildwars2.tools.alternator.tests;

public class AccountsSplitTest
{
    [Fact]
    public void Split_Blank_Blank()
    {
        var accounts = new List<IAccount>();
        var result = AccountCollection.SplitByVpn(accounts);
        result.Should().BeEmpty();
    }

    [Fact]
    public void Split_OneSet_OneAccount()
    {
        var accounts = CreateAccountList(new List<(List<string>, string)>
        {
            (new List<string>{ "V1" }, "account1"),
        });

        var result = AccountCollection.SplitByVpn(accounts);

        var expected = new Dictionary<string, List<IAccount>>
        {
            { "V1", new List<IAccount> { accounts[0] } },
        };
        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void Split_TwoSame_OneSetManyAccounts()
    {
        var accounts = CreateAccountList(new List<(List<string>, string)>
        {
            (new List<string>{ "V1" }, "account1"), 
            (new List<string>{ "V1" }, "account2"),
        });

        var result = AccountCollection.SplitByVpn(accounts);

        var expected = new Dictionary<string, List<IAccount>>
        {
            { "V1", new List<IAccount> { accounts[0], accounts[1] } },
        };
        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void Split_TwoDifferent_TwoSetSingleAccount()
    {
        var accounts = CreateAccountList(new List<(List<string>, string)>
        {
            (new List<string>{ "V1" }, "account1"),
            (new List<string>{ "V2" }, "account2"),
        });

        var result = AccountCollection.SplitByVpn(accounts);

        var expected = new Dictionary<string, List<IAccount>>
        {
            { "V1", new List<IAccount> { accounts[0] } },
            { "V2", new List<IAccount> { accounts[1] } },
        };
        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void Split_TwoBoth_TwoSetTwoAccounts()
    {
        var accounts = CreateAccountList(new List<(List<string>, string)>
        {
            (new List<string>{ "V1", "V2" }, "account1"),
            (new List<string>{ "V1", "V2" }, "account2"),
        });

        var result = AccountCollection.SplitByVpn(accounts);

        var expected = new Dictionary<string, List<IAccount>>
        {
            { "V1", new List<IAccount> { accounts[0], accounts[1] } },
            { "V2", new List<IAccount> { accounts[0], accounts[1] } },
        };
        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void Split_ManySingle_ManySetManyAccounts()
    {
        var accounts = CreateAccountList(new List<(List<string>, string)>
        {
            (new List<string>{ "V1" }, "account1"),
            (new List<string>{ "V1" }, "account2"),
            (new List<string>{ "V1" }, "account3"),
            (new List<string>{ "V2" }, "account4"),
            (new List<string>{ "V2" }, "account5"),
            (new List<string>{ "V3" }, "account6"),
        });

        var result = AccountCollection.SplitByVpn(accounts);

        var expected = new Dictionary<string, List<IAccount>>
        {
            { "V1", new List<IAccount> { accounts[0], accounts[1], accounts[2] } },
            { "V2", new List<IAccount> { accounts[3], accounts[4] } },
            { "V3", new List<IAccount> { accounts[5] } },
        };
        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void Split_Manymany_ManySetManyAccounts()
    {
        var accounts = CreateAccountList(new List<(List<string>, string)>
        {
            (new List<string>{ "V1" }, "account1"),
            (new List<string>{ "V1", "V2" }, "account2"),
            (new List<string>{ "V1", "V3" }, "account3"),
            (new List<string>{ "V2" }, "account4"),
            (new List<string>{ "V2", "V3" }, "account5"),
            (new List<string>{ "V3" }, "account6"),
        });

        var result = AccountCollection.SplitByVpn(accounts);

        var expected = new Dictionary<string, List<IAccount>>
        {
            { "V1", new List<IAccount> { accounts[0], accounts[1], accounts[2] } },
            { "V2", new List<IAccount> { accounts[1], accounts[3], accounts[4] } },
            { "V3", new List<IAccount> { accounts[2], accounts[4], accounts[5] } },
        };
        result.Should().BeEquivalentTo(expected);
    }

    private static List<IAccount> CreateAccountList(List<(List<string>, string)> names)
    {
        var accounts = new List<IAccount>();
        foreach (var postfix in names)
        {
            var account = Substitute.For<IAccount>();
            account.VPN.Returns(new ObservableCollectionEx<string>(postfix.Item1));
            account.Name.Returns(postfix.Item2);
            accounts.Add(account);
        }
        return accounts;
    }
}