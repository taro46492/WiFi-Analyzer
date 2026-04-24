using ManagedNativeWifi;

namespace WiFi_Analyzer.Models;

public class NetworkSecurityInfo
{
    public AuthenticationAlgorithm Authentication { get; set; }
    public CipherAlgorithm Encryption { get; set; }
}
