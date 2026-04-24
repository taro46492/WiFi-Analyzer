using ManagedNativeWifi;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Net;
using WiFi_Analyzer.Models;

namespace WiFi_Analyzer.Services.ConnectedNetwork;

public class ConnectedNetworkService : NetworkService, IConnectedNetworkService
{
    BssNetworkPack GetConnectedWlanBssEntry()
    {
        InterfaceConnectionInfo? connectedInterface = NativeWifi.EnumerateInterfaceConnections()
            .FirstOrDefault(connection => connection.IsConnected && connection.State == InterfaceState.Connected);

        if (connectedInterface is null)
            throw new Exception("No internet connection detected.");

        string? connectedSSID = NativeWifi.EnumerateAvailableNetworks()
            .Where(network => network.Interface.Id == connectedInterface.Id &&
                              network.ProfileName == connectedInterface.ProfileName)
            .Select(network => GetStringForSSID(network.Ssid))
            .FirstOrDefault();

        IEnumerable<BssNetworkPack> connectedBssEntries = NativeWifi.EnumerateBssNetworks()
            .Where(bssEntry => bssEntry.Interface.Id == connectedInterface.Id);

        if (!string.IsNullOrWhiteSpace(connectedSSID))
        {
            connectedBssEntries = connectedBssEntries
                .Where(bssEntry => GetStringForSSID(bssEntry.Ssid) == connectedSSID);
        }

        return connectedBssEntries.OrderByDescending(entry => entry.SignalStrength).FirstOrDefault()
            ?? throw new Exception("No internet connection detected.");
    }

    public NetworkStates GetConnectedNetworkStates()
    {
        BssNetworkPack connectedBssEntry = GetConnectedWlanBssEntry();

        long frequency = GetFrequencyFromChannel(connectedBssEntry.Frequency);
        int signalStrength = connectedBssEntry.SignalStrength;

        NetworkStates networkStates = new();

        networkStates.DistanceInMeters = CalculateDistance(signalStrength, frequency);
        networkStates.IsConnected = true;
        networkStates.SignalStrengthIndBm = signalStrength;

        return networkStates;
    }

    public WiFiNetwork GetConnectedWiFiNetwork()
    {
        BssNetworkPack connectedBssEntry = GetConnectedWlanBssEntry();

        WiFiNetwork wiFiNetwork = new();

        long frequency = GetFrequencyFromChannel(connectedBssEntry.Frequency);
        string currentSSID = GetStringForSSID(connectedBssEntry.Ssid);

        wiFiNetwork.SSID = currentSSID;
        wiFiNetwork.Channel = GetChannelFromFrequency(frequency);
        wiFiNetwork.FrequencyInHz = frequency;
        wiFiNetwork.Protocol = FindProtocolString(connectedBssEntry);
        wiFiNetwork.MacAddress = connectedBssEntry.Bssid.ToBytes();

        AvailableNetworkPack? wlanAvailableNetwork =
            GetWlanAvailableNetworkByProfileName(currentSSID, connectedBssEntry.Interface.Id);

        wiFiNetwork.IsSecured = wlanAvailableNetwork?.IsSecurityEnabled ?? false;
        wiFiNetwork.AuthenticationAlgorithm = wlanAvailableNetwork?.AuthenticationAlgorithm ?? AuthenticationAlgorithm.Unknown;

        return wiFiNetwork;
    }

    public async Task<IPAddressInfo> GetConnectedIPAddressInfo()
    {
        IPAddressInfo iPAddressInfo = new ();

        iPAddressInfo.PrivateIPv4 = GetPrivateIPv4();
        iPAddressInfo.PublicIPv4 = await GetPublicIPv4();
        iPAddressInfo.SubnetMask = GetSubnetMask();

        return iPAddressInfo;
    }

    string GetPrivateIPv4()
    {
        var host = Dns.GetHostEntry(Dns.GetHostName());
        var ipAddress = host.AddressList.FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork);
        return ipAddress!.ToString();
    }

    string GetSubnetMask()
    {
        var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
        foreach (var networkInterface in networkInterfaces)
        {
            if (networkInterface.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ||
                networkInterface.NetworkInterfaceType == NetworkInterfaceType.Ethernet)
            {
                foreach (var unicastIPAddressInformation in networkInterface.GetIPProperties().UnicastAddresses)
                {
                    if (unicastIPAddressInformation.Address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        return unicastIPAddressInformation.IPv4Mask!.ToString();
                    }
                }
            }
        }
        return null!;
    }

    async Task<string> GetPublicIPv4()
    {
        using (HttpClient httpClient = new())
            return (await httpClient.GetStringAsync("http://icanhazip.com")).Trim();
    }

    public NetworkSecurityInfo GetConnectedNetworkSecurityInfo()
    {
        NetworkSecurityInfo networkSecurityInfo = new();

        InterfaceConnectionInfo? connectedInterface = NativeWifi.EnumerateInterfaceConnections()
            .FirstOrDefault(connection => connection.IsConnected && connection.State == InterfaceState.Connected);

        if (connectedInterface is null)
            return networkSecurityInfo;

        AvailableNetworkPack? network = NativeWifi.EnumerateAvailableNetworks().FirstOrDefault(item =>
            item.Interface.Id == connectedInterface.Id &&
            item.ProfileName == connectedInterface.ProfileName);

        if (network is not null)
        {
            networkSecurityInfo.Authentication = network.AuthenticationAlgorithm;
            networkSecurityInfo.Encryption = network.CipherAlgorithm;
        }

        return networkSecurityInfo;
    }

    public NetworkInfrastructureInfo GetConnectedNetworkInfrastructureInfo()
    {
        NetworkInfrastructureInfo networkInfrastructureInfo = new();

        var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();

        var currentInterface = networkInterfaces.FirstOrDefault(nic => nic.OperationalStatus == OperationalStatus.Up);

        if (currentInterface != null)
        {
            networkInfrastructureInfo.InterfaceType = currentInterface.NetworkInterfaceType;
            networkInfrastructureInfo.OperationalStatus = currentInterface.OperationalStatus;
            networkInfrastructureInfo.Interface = currentInterface.Description;
        }

        return networkInfrastructureInfo;
    }
}
