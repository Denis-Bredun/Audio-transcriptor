
using System.Globalization;

namespace ShortNotes
{
    public partial class MainPage : ContentPage
    {
        private ISpeechToText _speechToText;
        private CancellationTokenSource _tokenSource;

        public Command ListenCommand { get; set; }
        public Command ListenCancelCommand { get; set; }
        public string RecognitionText { get; set; }

        public MainPage(ISpeechToText speechToText)
        {
            InitializeComponent();

            _speechToText = speechToText;
            _tokenSource = new CancellationTokenSource();

            ListenCommand = new Command(Listen);
            ListenCancelCommand = new Command(ListenCancel);
            BindingContext = this;
        }

        private async void Listen()
        {
            var isAuthorized = await _speechToText.RequestPermissionsAsync();

            if (isAuthorized)
            {
                try
                {
                    RecognitionText = await _speechToText.ListenAsync(
                        CultureInfo.GetCultureInfo("uk-UA"),
                        new Progress<string>(partialText =>
                        {
                            RecognitionText = partialText;
                            OnPropertyChanged(nameof(RecognitionText));
                        }), _tokenSource.Token);
                }
                catch (Exception ex)
                {
                    await DisplayAlert("Error", ex.Message, "OK");
                }
                _tokenSource = new CancellationTokenSource();
            }
            else
                await DisplayAlert("Permission Error", "No microphone access", "OK");
        }

        private void ListenCancel()
        {
            _tokenSource?.Cancel();
        }
    }
}
