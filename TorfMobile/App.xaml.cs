
namespace TorfMobile
{
    public partial class App : Application
    {
        FoilMagazine magazine;

        public App(FoilMagazine magazine)
        {
            InitializeComponent();
            this.magazine = magazine;
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            var window = new Window();

#if WINDOWS
            window.Page = new WinAppShell();

            window.Width = 1200;
            window.Height = 900;

            window.Destroying += (s, e) =>
            {
            
            };
#elif ANDROID
            window.Page = new AndAppShell();

#endif

            return window;
        }

        protected override void OnSleep()
        {
            base.OnSleep();
            
        }
    }
}