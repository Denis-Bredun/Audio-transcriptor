
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
                bool continueIfEditorHasText = await ContinueIfEditorHasText();

                if (continueIfEditorHasText)
                {
                    CleanEditorAndVariables();
                    await StartRecording();
                }
            }
            else
                StopRecording();
        }

        private async Task StartRecording()
        {
            var isAuthorized = await _speechToText.RequestPermissionsAsync();
            string lineBeforeSilence = "";

            if (isAuthorized)
            {
                try
                {
                    ChangeRecordButtonState(120, 120, 120, "Stop recording", true);

                    RecognitionText = await _speechToText.ListenAsync(
                        CultureInfo.GetCultureInfo("uk-UA"),
                        new Progress<string>(partialText =>
                        {
                            if (partialText.Length < lineBeforeSilence.Length)
                            {
                                RecognitionText += lineBeforeSilence;
                                lineBeforeSilence = "";
                            }

                            lineBeforeSilence += partialText.Substring(lineBeforeSilence.Length);

                            OnPropertyChanged(nameof(RecognitionText));
                        }), _tokenSource.Token);
                }
                catch (Exception ex)
                {
                    if (_tokenSource.IsCancellationRequested == true)
                    {
                        await DisplayAlert("Information", "Recording was stopped", "OK");
                        RecognitionText += lineBeforeSilence;
                        OnPropertyChanged(nameof(RecognitionText));
                        lineBeforeSilence = "";
                        _tokenSource = new CancellationTokenSource();
                    }
                    else
                        await DisplayAlert("Error", ex.Message, "OK");
                }
            }
            else
                await DisplayAlert("Permission Error", "No microphone access", "OK");
        }

        private void StopRecording()
        {
            ChangeRecordButtonState(0, 0, 0, "Start recording", false);
            _tokenSource?.Cancel();
        }

        private async Task<bool> ContinueIfEditorHasText()
        {
            if (string.IsNullOrEmpty(fieldForOutput.Text))
                return true;
            else
                return await DisplayAlert("Confirmation", "Text field contains text. "
                    + "The start of the new record will vanish last text."
                    + "Do you want to continue?", "Yes", "No");
        }

        private void CleanEditorAndVariables()
        {
            fieldForOutput.Text = "";
            RecognitionText = "";
        }

        private void ChangeRecordButtonState(int red, int green, int blue, string Text, bool isRecordingState)
        {
            startRecordingBt.BackgroundColor = Color.FromRgb(red, green, blue);
            startRecordingBt.Text = Text;
            _isRecording = isRecordingState;
        }
    }
}
