using Microsoft.Extensions.Logging;
using TorfMobile.Network;
using TorfMobile.ViewModels;

namespace TorfMobile
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

#if DEBUG
    		builder.Logging.AddDebug();
#endif

            builder.Services.AddSingleton<FoilMagazine>();
            builder.Services.AddSingleton<DataBase>();

            builder.Services.AddSingleton<NetworkService>();
            builder.Services.AddSingleton<DeviceConnectionViewModel>();

            return builder.Build();
        }
    }
}
