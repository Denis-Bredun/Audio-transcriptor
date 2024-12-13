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
        public Command AddToClipboardCommand { get; set; }
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
            AddToClipboardCommand = new Command(AddToClipboard);
            BindingContext = this;

            UpdateStopwatchValues("00", "00", "00");
        }

        private async void AddToClipboard()
        {
            await Clipboard.SetTextAsync(fieldForOutput.Text);
        }

        private async void RecordAudio()
        {
            if (!CheckInternetConnection())
            {
                await DisplayAlert("Помилка", "Немає Інтернет-підключення", "OK");
                return;
            }
            if (_isRecording == false)
            {
                bool shouldBeVanished = await VanishLastTextOrContinue();

                if (shouldBeVanished)
                    CleanEditor();

                await StartRecording();
            }
            else
                StopRecording();
        }

        private bool CheckInternetConnection() => Connectivity.Current.NetworkAccess == NetworkAccess.Internet;

        private async Task StartRecording()
        {
            var isAuthorized = await _speechToText.RequestPermissionsAsync();

            if (isAuthorized)
            {
                try
                {
                    ChangeRecordButtonState(120, 120, 120, "Зупинити запис", true, true);

                    ResetStopwatch();

                    await ListenToSpeech();
                }
                catch (Exception ex)
                {
                    StopStopwatch();

                    if (_tokenSource.IsCancellationRequested == true)
                        await ActionAfterStoppingRecording();
                    else
                        await DisplayAlert("Помилка", ex.Message, "OK");

                    ChangeRecordButtonState(0, 0, 0, "Розпочати запис", false, false);
                }
            }
            else
                await DisplayAlert("Помилка дозволу", "Немає доступу до мікрофону", "OK");
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
            await DisplayAlert("Інформація", "Запис був зупинений", "OK");
            _tokenSource = new CancellationTokenSource();
        }

        private void StopRecording()
        {
            _tokenSource?.Cancel();
        }

        private async Task<bool> VanishLastTextOrContinue()
        {
            if (string.IsNullOrEmpty(fieldForOutput.Text))
                return true;
            else
                return await DisplayAlert("Питання", "Бажаєте стерти останній текст чи продовжити запис?", "Стерти", "Продовжити");
        }

        private void CleanEditor() => RecognitionText = "";

        private void ChangeRecordButtonState(int red, int green, int blue, string Text, bool isRecordingState, bool isTextFieldReadonly)
        {
            startRecordingBt.BackgroundColor = Color.FromRgb(red, green, blue);
            startRecordingBt.Text = Text;
            fieldForOutput.IsReadOnly = isTextFieldReadonly;
            _isRecording = isRecordingState;
        }
    }
}
