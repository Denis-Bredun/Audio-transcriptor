
using System.Globalization;

namespace ShortNotes
{
    public partial class MainPage : ContentPage
    {
        private ISpeechToText _speechToText;
        private CancellationTokenSource _tokenSource;
        private bool _isRecording;

        public Command RecordAudioCommand { get; set; }
        public string RecognitionText { get; set; }

        public MainPage(ISpeechToText speechToText)
        {
            InitializeComponent();

            _speechToText = speechToText;
            _tokenSource = new CancellationTokenSource();
            _isRecording = false;

            RecordAudioCommand = new Command(RecordAudio);
            BindingContext = this;
        }

        private async void RecordAudio()
        {
            if (_isRecording == false)
            {
                var isAuthorized = await _speechToText.RequestPermissionsAsync();

                if (isAuthorized)
                {
                    try
                    {
                        _isRecording = true;
                        startRecordingBt.Text = "Stop recording";
                        startRecordingBt.BackgroundColor = Color.FromRgb(122, 105, 104);

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
                        if (_tokenSource.IsCancellationRequested == true)
                        {
                            await DisplayAlert("Information", "Recording was stopped", "OK");
                            _tokenSource = new CancellationTokenSource();
                        }
                        else
                            await DisplayAlert("Error", ex.Message, "OK");
                    }
                }
                else
                    await DisplayAlert("Permission Error", "No microphone access", "OK");
            }
            else
            {
                _isRecording = false;
                startRecordingBt.Text = "Start recording";
                startRecordingBt.BackgroundColor = Color.FromRgb(36, 31, 30);

                _tokenSource?.Cancel();
            }
        }
    }
}
