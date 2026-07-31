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
            var window = new Window(new AppShell());

#if WINDOWS
            window.Width = 600;
            window.Height = 900;

            window.Destroying += (s, e) =>
            {
            magazine.CloseDataBase();
            };
#endif

            return window;
        }

        protected override void OnSleep()
        {
            base.OnSleep();
            magazine.CloseDataBase();
        }
    }
}