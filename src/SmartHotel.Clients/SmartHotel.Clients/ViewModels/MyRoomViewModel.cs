﻿using SmartHotel.Clients.Core.Services.Analytic;
using SmartHotel.Clients.Core.Services.OpenUri;
using SmartHotel.Clients.Core.ViewModels.Base;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Input;
using SmartHotel.Clients.Core.Helpers;
using SmartHotel.Clients.Core.Services.IoT;
using Xamarin.Forms;

namespace SmartHotel.Clients.Core.ViewModels
{
    public class MyRoomViewModel : ViewModelBase, IHandleViewAppearing, IHandleViewDisappearing
    {
        const string Skype = "Skype";
        const string FacebookMessenger = "Facebook Messenger";

        private double _desiredAmbientLight;
        private double _currentAmbientLight;
        private double _ambientLightMinimum = RoomAmbientLight.DefaultMinimum.RawValue;
        private double _ambientLightMaximum = RoomAmbientLight.DefaultMaximum.RawValue;
        private double _desiredTemperature;
        private double _currentTemperature;
        private double _temperatureMinimum = RoomTemperature.DefaultMinimum.RawValue;
        private double _temperatureMaximum = RoomTemperature.DefaultMaximum.RawValue;
        private double _musicVolume;
        private double _windowBlinds;
        private bool _isEcoMode;
        private bool _ambient;
        private bool _need;
        private bool _find;
        private bool _noDisturb;

	    private bool _isInitialized;

	    private readonly TimeSpan _sliderInertia = TimeSpan.FromSeconds( 1 );
	    private readonly Timer _delayedTemperatureChangedTimer;
	    private readonly Timer _delayedLightChangedTimer;

        private readonly IOpenUriService _openUrlService;
        private readonly IAnalyticService _analyticService;
        private readonly IRoomDevicesDataService _roomDevicesDataService;

        public MyRoomViewModel(
            IOpenUriService openUrlService,
            IAnalyticService analyticService,
            IRoomDevicesDataService roomDevicesDataService)
        {
            _openUrlService = openUrlService;
            _analyticService = analyticService;
            _roomDevicesDataService = roomDevicesDataService;


	        _delayedTemperatureChangedTimer = new Timer( _sliderInertia,
		        async () => { await UpdateRoomTemperature( DesiredTemperature ); } );

	        _delayedLightChangedTimer = new Timer( _sliderInertia,
		        async () => { await UpdateRoomLight( DesiredAmbientLight ); } );

            SetNeed();
        }

        public bool UseRealRoomDevices => !_roomDevicesDataService.UseFakes;

        public double CurrentAmbientLight
        {
            get => _currentAmbientLight;
            set
            {
                _currentAmbientLight = value;
                OnPropertyChanged();
            }
        }

        public double DesiredAmbientLight
        {
            get => _desiredAmbientLight;
            set
            {
	            if (Math.Abs(_desiredAmbientLight - value) < double.Epsilon)
	            {
		            return;
	            }

	            _desiredAmbientLight = Math.Round(value);
	            OnPropertyChanged();

	            if ( IsRoomDevicesLive() )
	            {
		            _delayedLightChangedTimer.Stop();
		            _delayedLightChangedTimer.Start();
	            }
            }
        }

        public double AmbientLightMinimum
        {
            get => _ambientLightMinimum;
            set
            {
                _ambientLightMinimum = value;
                OnPropertyChanged();
            }
        }

        public double AmbientLightMaximum
        {
            get => _ambientLightMaximum;
            set
            {
                _ambientLightMaximum = value;
                OnPropertyChanged();
            }
        }

        public double DesiredTemperature
        {
            get => _desiredTemperature;
            set
            {
                if (Math.Abs(_desiredTemperature - value) < double.Epsilon)
                {
                    return;
                }

                _desiredTemperature = Math.Round(value);
	            OnPropertyChanged();

	            if ( IsRoomDevicesLive() )
	            {
		            _delayedTemperatureChangedTimer.Stop();
		            _delayedTemperatureChangedTimer.Start();
	            }
            }
        }

        public double CurrentTemperature
        {
            get => _currentTemperature;
            set
            {
                _currentTemperature = value;
                OnPropertyChanged();
            }
        }

        public double TemperatureMinimum
        {
            get => _temperatureMinimum;
            set
            {
                _temperatureMinimum = value;
                OnPropertyChanged();
            }
        }

        public double TemperatureMaximum
        {
            get => _temperatureMaximum;
            set
            {
                _temperatureMaximum = value;
                OnPropertyChanged();
            }
        }

        public double MusicVolume
        {
            get { return _musicVolume; }
            set
            {
                _musicVolume = value;
                OnPropertyChanged();
            }
        }

        public double WindowBlinds
        {
            get { return _windowBlinds; }
            set
            {
                _windowBlinds = value;
                OnPropertyChanged();
            }
        }

        public bool IsEcoMode
        {
            get { return _isEcoMode; }
            set
            {
                _isEcoMode = value;
                OnPropertyChanged();
            }
        }

        public bool Ambient
        {
            get { return _ambient; }
            set
            {
                _ambient = value;
                OnPropertyChanged();
            }
        }

        public bool Need
        {
            get { return _need; }
            set
            {
                _need = value;
                OnPropertyChanged();
            }
        }

        public bool Find
        {
            get { return _find; }
            set
            {
                _find = value;
                OnPropertyChanged();
            }
        }

        public bool NoDisturb
        {
            get { return _noDisturb; }
            set
            {
                _noDisturb = value;
                OnPropertyChanged();
            }
        }

        public ICommand AmbientCommand => new Command(SetAmbient);

        public ICommand NeedCommand => new Command(SetNeed);

        public ICommand FindCommand => new Command(SetFind);

        public ICommand OpenDoorCommand => new AsyncCommand(OpenDoorAsync);

        public ICommand CheckoutCommand => new AsyncCommand(CheckoutAsync);

        public ICommand OpenBotCommand => new AsyncCommand(OpenBotAsync);

        public ICommand EcoModeCommand => new Command(EcoMode);

        public override async Task InitializeAsync(object navigationData)
        {
            IsBusy = true;

            IsEcoMode = false;

            await GetRoomSensorData(true);

            MusicVolume = 45;
            WindowBlinds = 80;

            IsBusy = false;
	        _isInitialized = true;
        }

	    private async Task GetRoomSensorData( bool isInitializing = false )
	    {
		    RoomTemperature roomTemperature = await _roomDevicesDataService.GetRoomTemperatureAsync();
		    CurrentTemperature = roomTemperature.Value.RawValue;

		    RoomAmbientLight roomAmbientLight = await _roomDevicesDataService.GetRoomAmbientLightAsync();
		    CurrentAmbientLight = roomAmbientLight.Value.RawValue;

		    if ( isInitializing )
		    {
			    TemperatureMaximum = roomTemperature.Maximum.RawValue;
			    TemperatureMinimum = roomTemperature.Minimum.RawValue;
			    DesiredTemperature = roomTemperature.Desired.RawValue;

			    AmbientLightMaximum = roomAmbientLight.Maximum.RawValue;
			    AmbientLightMinimum = roomAmbientLight.Minimum.RawValue;
			    DesiredAmbientLight = roomAmbientLight.Desired.RawValue;
		    }
	    }

        public Task OnViewAppearingAsync(VisualElement view)
        {
	        _roomDevicesDataService.SensorDataChanged += RoomDevicesDataServiceSensorDataChanged;
	        _roomDevicesDataService.StartCheckingRoomSensorData();

            return Task.FromResult(true);
        }

        public Task OnViewDisappearingAsync(VisualElement view)
        {
	        _roomDevicesDataService.SensorDataChanged -= RoomDevicesDataServiceSensorDataChanged;
            _roomDevicesDataService.StopCheckingRoomSensorData();

            return Task.FromResult(true);
        }

	    private async void RoomDevicesDataServiceSensorDataChanged( object sender, EventArgs e )
	    {
		    await GetRoomSensorData();
	    }

        private bool IsRoomDevicesLive()
        {
            return _isInitialized && UseRealRoomDevices;
        }

        private async Task UpdateRoomLight(double desiredAmbientLight)
        {
	        _delayedLightChangedTimer.Stop();

            Debug.WriteLine($"UpdateRoomLight: {desiredAmbientLight}");
            await _roomDevicesDataService.UpdateDesiredAsync((float)desiredAmbientLight / 100f, SensorDataType.Light);
        }

        private async Task UpdateRoomTemperature(double desiredTemperature)
        {
	        _delayedTemperatureChangedTimer.Stop();

			Debug.WriteLine($"UpdateRoomTemperature: {desiredTemperature}");
            await _roomDevicesDataService.UpdateDesiredAsync((float)desiredTemperature, SensorDataType.Temperature);
        }

        private void SetAmbient()
        {
            Ambient = true;
            Need = false;
            Find = false;
        }

        private void SetNeed()
        {
            Ambient = false;
            Need = true;
            Find = false;
        }

        private void SetFind()
        {
            Ambient = false;
            Need = false;
            Find = true;
        }

        private Task OpenDoorAsync()
        {
            return NavigationService.NavigateToPopupAsync<OpenDoorViewModel>(true);
        }

        private Task CheckoutAsync()
        {
            return NavigationService.NavigateToPopupAsync<CheckoutViewModel>(true);
        }

        private async Task OpenBotAsync()
        {
            var bots = new[] { Skype, FacebookMessenger };

            try
            {
                var selectedBot =
                    await DialogService.SelectActionAsync(
                        Resources.BotSelectionMessage,
                        Resources.BotSelectionTitle,
                        bots);

                switch (selectedBot)
                {
                    case Skype:
                        _openUrlService.OpenSkypeBot(AppSettings.SkypeBotId);
                        _analyticService.TrackEvent("SkypeBot");
                        break;
                    case FacebookMessenger:
                        _openUrlService.OpenFacebookBot(AppSettings.FacebookBotId);
                        _analyticService.TrackEvent("FacebookBot");
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OpenBot: {ex}");

                await DialogService.ShowAlertAsync(
                  Resources.BotError,
                  Resources.ExceptionTitle,
                  Resources.DialogOk);
            }
        }

        private void EcoMode()
        {
            if (IsEcoMode)
                ActivateDefaultMode(true);
            else
                ActivateEcoMode(true);
        }

        private void ActivateDefaultMode(bool showToast = false)
        {
            IsEcoMode = false;

            DesiredAmbientLight = 100;
            DesiredTemperature = 70;
            MusicVolume = 45;
            WindowBlinds = 80;

            if (showToast)
            {
                DialogService.ShowToast(Resources.DeactivateEcoMode, 1000);
            }
        }

        private void ActivateEcoMode(bool showToast = false)
        {
            IsEcoMode = true;

            DesiredAmbientLight = 0;
            DesiredTemperature = 60;
            MusicVolume = 40;
            WindowBlinds = 50;

            if (showToast)
            {
                DialogService.ShowToast(Resources.ActivateEcoMode, 1000);
            }
        }
    }
}