using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using System.Diagnostics;
using System.Globalization;

namespace ShortNotes
{
    public partial class MainPage : ContentPage
    {
        private ISpeechToText _speechToText;
        private CancellationTokenSource _tokenSource;
        private bool _isRecording;
        private Stopwatch _stopwatch;
        private bool _isEditable;
        private string _selectedLanguage;

        public Command RecordAudioCommand { get; set; }
        public Command AddToClipboardCommand { get; set; }
        public Command CleanCommand { get; set; }
        public Command EditCommand { get; set; }
        public string RecognitionText { get; set; }
        public string StopWatchHours { get; set; }
        public string StopWatchMinutes { get; set; }
        public string StopWatchSeconds { get; set; }

        public string SelectedLanguage
        {
            get => _selectedLanguage;
            set
            {
                if (_selectedLanguage != value)
                {
                    _selectedLanguage = value;
                    OnPropertyChanged(nameof(SelectedLanguage));
                }
            }
        }

        public List<string> Languages { get; } = new List<string>
        {
            "Українська",
            "Англійська",
            "Російська"
        };

        public MainPage(ISpeechToText speechToText)
        {
            InitializeComponent();

            _speechToText = speechToText;
            _tokenSource = new CancellationTokenSource();
            _isRecording = false;
            _stopwatch = new Stopwatch();

            RecordAudioCommand = new Command(RecordAudio);
            AddToClipboardCommand = new Command(AddToClipboard);
            CleanCommand = new Command(CleanEditor);
            EditCommand = new Command(ChangeEditMode);
            BindingContext = this;

            SelectedLanguage = Languages.First();

            UpdateStopwatchValues("00", "00", "00");
        }

        private CultureInfo GetCultureInfoFromLanguage(string language)
        {
            return language switch
            {
                "Українська" => CultureInfo.GetCultureInfo("uk-UA"),
                "Англійська" => CultureInfo.GetCultureInfo("en-US"),
                "Російська" => CultureInfo.GetCultureInfo("ru-RU"),
                _ => CultureInfo.InvariantCulture
            };
        }

        private async Task ShowToastAsync(string message)
        {
            var toast = Toast.Make(message, ToastDuration.Short, 20);
            await toast.Show();
        }

        private async Task<bool> ShowConfirmationDialogAsync(string title, string message, string accept, string cancel)
        {
            return await DisplayAlert(title, message, accept, cancel);
        }

        private async void AddToClipboard()
        {
            await Clipboard.SetTextAsync(fieldForOutput.Text);
            await ShowToastAsync("Дані успішно скопійовані!");
        }

        private async void CleanEditor()
        {
            var isCleaned = await ShowConfirmationDialogAsync("Питання", "Ви дійсно бажаєте стерти текст та онулити час?", "Так", "Ні");

            if (isCleaned)
            {
                RecognitionText = "";
                OnPropertyChanged(nameof(RecognitionText));
                UpdateStopwatchValues("00", "00", "00");
                await ShowToastAsync("Текст був успішно стертий!");
            }
        }

        private async Task SetEditModeAsync(bool isEditable, string toastMessage, string iconSource)
        {
            fieldForOutput.IsReadOnly = !isEditable;
            editButton.Source = iconSource;
            _isEditable = isEditable;
            await ShowToastAsync(toastMessage);
        }

        private async void ChangeEditMode()
        {
            if (_isEditable)
            {
                await SetEditModeAsync(false, "Ви вийшли з режиму редагування.", "not_editing.png");
            }
            else
            {
                _isEditable = await ShowConfirmationDialogAsync("Питання", "Бажаєте редагувати текст?", "Так", "Ні");

                if (_isEditable)
                {
                    await SetEditModeAsync(true, "Ви увійшли в режим редагування.", "is_editing.png");
                }
            }
        }

        private async Task<bool> CheckInternetConnection()
        {
            var isConnected = Connectivity.Current.NetworkAccess == NetworkAccess.Internet;

            if (isConnected == false)
                await ShowToastAsync("Немає Інтернет-підключення");

            return isConnected;
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

        private async void RecordAudio()
        {
            var isConnected = await CheckInternetConnection();

            if (!isConnected) return;

            if (_isRecording == false)
                await StartRecording();
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
                    _isRecording = true;
                    recordButton.Source = "pause.png";
                    ChangeStateOfButtonsWhenRecording(_isRecording);

                    StartStopwatch();

                    await ListenToSpeech();
                }
                catch (Exception ex)
                {
                    StopStopwatch();

                    if (_tokenSource.IsCancellationRequested == true)
                        await ActionAfterStoppingRecording();
                    else
                        await ShowToastAsync(ex.Message);

                    _isRecording = false;
                    recordButton.Source = "start.png";
                    ChangeStateOfButtonsWhenRecording(_isRecording);
                }
            }
            else
                await ShowToastAsync("Немає доступу до мікрофону");
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

        private void StopStopwatch()
        {
            if (_stopwatch.IsRunning)
                _stopwatch.Stop();
        }

        private async Task ListenToSpeech()
        {
            var culture = GetCultureInfoFromLanguage(SelectedLanguage);

            string constantPart = "";
            bool putNewLine = false;

            RecognitionText = await _speechToText.ListenAsync(
                culture,
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

        private bool DoesntConstantPartContainTransferingToNewLine(string constantPart) => constantPart != null && constantPart.Length > 0 && constantPart[constantPart.Length - 1] != '\n' && constantPart != "";

        private async Task ActionAfterStoppingRecording()
        {
            await ShowToastAsync("Запис був зупинений");
            _tokenSource = new CancellationTokenSource();
        }

        private void StopRecording()
        {
            _tokenSource?.Cancel();
        }

        private async void ChangeStateOfButtonsWhenRecording(bool isRecording)
        {
            languagePicker.IsEnabled = !isRecording;
            cleanButton.IsEnabled = !isRecording;
            editButton.IsEnabled = !isRecording;
            copyButton.IsEnabled = !isRecording;

            if (_isEditable)
                await SetEditModeAsync(false, "Ви вийшли з режиму редагування.", "not_editing.png");
        }
    }
}
