namespace guildwars2.tools.alternator.MVVM.model;

[Serializable]
public class Settings : ObservableObject
{

    private string? gw2Folder;
    public string? Gw2Folder
    {
        get => gw2Folder;
        set => SetProperty(ref gw2Folder, value);
    }

    private int maxLoginInstances;
    public int MaxLoginInstances
    {
        get => maxLoginInstances;
        set => SetProperty(ref maxLoginInstances, value);
    }

    private int accountBand1;
    public int AccountBand1
    {
        get => accountBand1;
        set => SetProperty(ref accountBand1, value);
    }
    private int accountBand1Delay;
    public int AccountBand1Delay
    {
        get => accountBand1Delay;
        set => SetProperty(ref accountBand1Delay, value);
    }

    private int accountBand2;
    public int AccountBand2
    {
        get => accountBand2;
        set => SetProperty(ref accountBand2, value);
    }
    private int accountBand2Delay;
    public int AccountBand2Delay
    {
        get => accountBand2Delay;
        set => SetProperty(ref accountBand2Delay, value);
    }

    private int accountBand3;
    public int AccountBand3
    {
        get => accountBand3;
        set => SetProperty(ref accountBand3, value);
    }
    private int accountBand3Delay;
    public int AccountBand3Delay
    {
        get => accountBand3Delay;
        set => SetProperty(ref accountBand3Delay, value);
    }

    private int stuckTimeout;
    public int StuckTimeout
    {
        get => stuckTimeout;
        set => SetProperty(ref stuckTimeout, value);
    }

    private int vpnAccountCount;
    public int VpnAccountCount
    {
        get => vpnAccountCount;
        set => SetProperty(ref vpnAccountCount, value);
    }

    private ErrorDetection experimentalErrorDetection;
    public ErrorDetection ExperimentalErrorDetection
    {
        get => experimentalErrorDetection;
        set => SetProperty(ref experimentalErrorDetection, value);
    }

    private bool alwaysIgnoreVpn;
    public bool AlwaysIgnoreVpn
    {
        get => alwaysIgnoreVpn;
        set => SetProperty(ref alwaysIgnoreVpn, value);
    }

}