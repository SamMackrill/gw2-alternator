namespace guildwars2.tools.alternator.tests;

public class AccountsSplitTest
{
    [Fact]
    public void Split_Blank_Blank()
    {
        var accounts = new List<IAccount>();
        var result = AccountCollection.AccountsByVpn(accounts, false);
        result.Should().BeEmpty();
    }

    [Fact]
    public void Split_OneSet_OneAccount()
    {
        var accounts = CreateAccountList(new List<(List<string>?, string)>
        {
            (new List<string>{ "V1" }, "account1"),
        });

        var result = AccountCollection.AccountsByVpn(accounts, false);

        var expected = new Dictionary<string, List<IAccount>>
            {
                { "V1", new List<IAccount> { accounts[0] } },
            };
        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void Split_TwoSame_OneSetManyAccounts()
    {
        var accounts = CreateAccountList(new List<(List<string>?, string)>
        {
            (new List<string>{ "V1" }, "account1"), 
            (new List<string>{ "V1" }, "account2"),
        });

        var result = AccountCollection.AccountsByVpn(accounts, false);

        var expected = new Dictionary<string, List<IAccount>>
        {
            { "V1", new List<IAccount> { accounts[0], accounts[1] } },
        };
        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void Split_TwoDifferent_TwoSetSingleAccount()
    {
        var accounts = CreateAccountList(new List<(List<string>?, string)>
        {
            (new List<string>{ "V1" }, "account1"),
            (new List<string>{ "V2" }, "account2"),
        });

        var result = AccountCollection.AccountsByVpn(accounts, false);

        var expected = new Dictionary<string, List<IAccount>>
        {
            { "V1", new List<IAccount> { accounts[0] } },
            { "V2", new List<IAccount> { accounts[1] } },
        };
        result.Should().BeEquivalentTo(expected);
    }

    //[Fact]
    //public void Split_TwoBoth_TwoSetTwoAccounts()
    //{
    //    var accounts = CreateAccountList(new List<(List<string>?, string)>
    //    {
    //        (new List<string>{ "V1", "V2" }, "account1"),
    //        (new List<string>{ "V1", "V2" }, "account2"),
    //    });

    //    var result = AccountCollection.AccountsByVpn(accounts, false);

    //    var expected = new Dictionary<string, List<Client>>
    //    {
    //        { "V1", new List<Client> { accounts[0].Client, accounts[1].Client } },
    //        { "V2", new List<Client> { accounts[0].Client, accounts[1].Client } },
    //    };
    //    result.Should().BeEquivalentTo(expected);
    //}

    //[Fact]
    //public void Split_ManySingle_ManySetManyAccounts()
    //{
    //    var accounts = CreateAccountList(new List<(List<string>?, string)>
    //    {
    //        (new List<string>{ "V1" }, "account1"),
    //        (new List<string>{ "V1" }, "account2"),
    //        (new List<string>{ "V1" }, "account3"),
    //        (new List<string>{ "V2" }, "account4"),
    //        (new List<string>{ "V2" }, "account5"),
    //        (new List<string>{ "V3" }, "account6"),
    //    });

    //    var result = AccountCollection.AccountsByVpn(accounts, false);

    //    var expected = new Dictionary<string, List<Client>>
    //    {
    //        { "V1", new List<Client> { accounts[0].Client, accounts[1].Client, accounts[2].Client } },
    //        { "V2", new List<Client> { accounts[3].Client, accounts[4].Client } },
    //        { "V3", new List<Client> { accounts[5].Client } },
    //    };
    //    result.Should().BeEquivalentTo(expected);
    //}

    //[Fact]
    //public void Split_Manymany_ManySetManyAccounts()
    //{
    //    var accounts = CreateAccountList(new List<(List<string>?, string)>
    //    {
    //        (new List<string>{ "V1" }, "account1"),
    //        (new List<string>{ "V1", "V2" }, "account2"),
    //        (new List<string>{ "V1", "V3" }, "account3"),
    //        (new List<string>{ "V2" }, "account4"),
    //        (new List<string>{ "V2", "V3" }, "account5"),
    //        (new List<string>{ "V3" }, "account6"),
    //    });

    //    var result = AccountCollection.AccountsByVpn(accounts, false);

    //    var expected = new Dictionary<string, List<Client>>
    //    {
    //        { "V1", new List<Client> { accounts[0].Client, accounts[1].Client, accounts[2].Client } },
    //        { "V2", new List<Client> { accounts[1].Client, accounts[3].Client, accounts[4].Client } },
    //        { "V3", new List<Client> { accounts[2].Client, accounts[4].Client, accounts[5].Client } },
    //    };
    //    result.Should().BeEquivalentTo(expected);
    //}

    //[Fact]
    //public void Split_ManyManyPlusEmpty_ManySetManyAccounts()
    //{
    //    var accounts = CreateAccountList(new List<(List<string>?, string)>
    //    {
    //        (new List<string>{ "V1" }, "account1"),
    //        (new List<string>{ "V1", "V2" }, "account2"),
    //        (new List<string>{ "V1", "V3" }, "account3"),
    //        (new List<string>{ "V2" }, "account4"),
    //        (new List<string>{ "V2", "V3" }, "account5"),
    //        (new List<string>{ "V3" }, "account6"),
    //        (null , "account7"),
    //    });

    //    var result = AccountCollection.AccountsByVpn(accounts, false);

    //    var expected = new Dictionary<string, List<Client>>
    //    {
    //        { "V1", new List<Client> { accounts[0].Client, accounts[1].Client, accounts[2].Client } },
    //        { "V2", new List<Client> { accounts[1].Client, accounts[3].Client, accounts[4].Client } },
    //        { "V3", new List<Client> { accounts[2].Client, accounts[4].Client, accounts[5].Client } },
    //        { "", new List<Client> { accounts[6].Client } },
    //    };
    //    result.Should().BeEquivalentTo(expected);
    //}

    private static List<IAccount> CreateAccountList(List<(List<string>?, string)> names)
    {
        var accounts = new List<IAccount>();
        foreach (var postfix in names)
        {
            var account = new Account(postfix.Item2);
            account.VPN = postfix.Item1 == null ? null : new ObservableCollectionEx<string>(postfix.Item1);
            accounts.Add(account);
        }
        return accounts;
    }
}