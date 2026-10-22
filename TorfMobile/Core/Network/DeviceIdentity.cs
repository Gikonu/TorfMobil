using Microsoft.Maui.Devices;
using Microsoft.Maui.Storage;

namespace TorfMobile.Network;

/// <summary>Stały identyfikator instalacji aplikacji.</summary>
public static class DeviceIdentity
{
    private const string IdKey = "TorfMobile.Network.DeviceId";

    public static LanDevice GetLocalDevice()
    {
        string id = Preferences.Default.Get(IdKey, "");
        if (string.IsNullOrWhiteSpace(id))
        {
            id = Guid.NewGuid().ToString("N");
            Preferences.Default.Set(IdKey, id);
        }

        return new LanDevice
        {
            Id = id,
            Name = DeviceInfo.Current.Name,
            Platform = DeviceInfo.Current.Platform.ToString(),
            TcpPort = NetworkSettings.TcpPort
        };
    }
}
