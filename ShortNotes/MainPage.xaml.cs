using System.Diagnostics;
using System.Globalization;

namespace ShortNotes
{
    public partial class MainPage : ContentPage
    {
        private ISpeechToText _speechToText;
        private CancellationTokenSource _tokenSource;
        private bool _isRecording;
        private string _stopWatchHours, _stopWatchMinutes, _stopWatchSeconds;
        private Stopwatch _stopwatch;

        public Command RecordAudioCommand { get; set; }
        public string RecognitionText { get; set; }
        public string StopWatchHours { get; set; }
        public string StopWatchMinutes { get; set; }
        public string StopWatchSeconds { get; set; }

        public MainPage(ISpeechToText speechToText)
        {
            InitializeComponent();

            _speechToText = speechToText;
            _tokenSource = new CancellationTokenSource();
            _isRecording = false;
            _stopwatch = new Stopwatch();

            RecordAudioCommand = new Command(RecordAudio);
            BindingContext = this;

            UpdateStopwatchValues("00", "00", "00");
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

            if (isAuthorized)
            {
                try
                {
                    ChangeRecordButtonState(120, 120, 120, "Stop recording", true);

                    ResetStopwatch();

                    await ListenToSpeech();
                }
                catch (Exception ex)
                {
                    StopStopwatch();

                    if (_tokenSource.IsCancellationRequested == true)
                        await ActionAfterStoppingRecording();
                    else
                        await DisplayAlert("Error", ex.Message, "OK");
                }
            }
            else
                await DisplayAlert("Permission Error", "No microphone access", "OK");
        }

        private void StartStopwatch()
        {
            _stopwatch.Start();
            Task.Run(async () =>
            {
                while (_stopwatch.IsRunning && Application.Current != null)
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        UpdateStopwatchValues(_stopwatch.Elapsed.Hours.ToString("D2"),
                                              _stopwatch.Elapsed.Minutes.ToString("D2"),
                                              _stopwatch.Elapsed.Seconds.ToString("D2"));
                    });

                    await Task.Delay(1000);
                }
            });
        }

        private void UpdateStopwatchValues(string hours, string minutes, string seconds)
        {
            StopWatchHours = hours;
            OnPropertyChanged("StopWatchHours");
            StopWatchMinutes = minutes;
            OnPropertyChanged("StopWatchMinutes");
            StopWatchSeconds = seconds;
            OnPropertyChanged("StopWatchSeconds");
        }

        private void ResetStopwatch()
        {
            StopStopwatch();
            _stopwatch.Reset();
            StartStopwatch();
        }

        private void StopStopwatch()
        {
            if (_stopwatch.IsRunning)
                _stopwatch.Stop();
        }

        private async Task ListenToSpeech()
        {
            string constantPart = "";
            bool putNewLine = false;

            RecognitionText = await _speechToText.ListenAsync(
                        CultureInfo.GetCultureInfo("uk-UA"),
                        new Progress<string>(partialText =>
                        {
                            FormatRecognizedSpeech(ref constantPart, ref partialText, ref putNewLine);

                            OnPropertyChanged(nameof(RecognitionText));
                        }), _tokenSource.Token);
        }

        private void FormatRecognizedSpeech(ref string constantPart, ref string partialText, ref bool putNewLine)
        {
            if (partialText == "")
            {
                constantPart = RecognitionText;
                putNewLine = true;
            }

            if (putNewLine)
            {
                if (DoesntConstantPartContainTransferingToNewLine(constantPart))
                    constantPart += ". \n";

                RecognitionText = $"{constantPart}{partialText}";
                putNewLine = false;
            }
            else
                RecognitionText = $"{constantPart}{partialText}";
        }

        private bool DoesntConstantPartContainTransferingToNewLine(string constantPart) => constantPart.Length > 0 && constantPart[constantPart.Length - 1] != '\n' && constantPart != "";

        private async Task ActionAfterStoppingRecording()
        {
            await DisplayAlert("Information", "Recording was stopped", "OK");
            _tokenSource = new CancellationTokenSource();
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
