namespace ShortNotes
{
    public partial class App : Application
    {
        public App(MainPage mainPage)
        {
            InitializeComponent();

            MainPage = mainPage;
            //MainPage = new AppShell();
        }
    }
}
