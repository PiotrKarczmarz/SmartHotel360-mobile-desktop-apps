using SmartHotel.Clients.Core.Models;
using SmartHotel.Clients.Core.Services.Authentication;
using SmartHotel.Clients.Core.Services.Booking;
using SmartHotel.Clients.Core.Services.Chart;
using SmartHotel.Clients.Core.Services.Notification;
using SmartHotel.Clients.Core.ViewModels.Base;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Input;
using Microcharts;
using SkiaSharp;
using SmartHotel.Clients.Core.Controls;
using SmartHotel.Clients.Core.Services.IoT;
using Xamarin.Forms;
using Entry = Microcharts.Entry;

namespace SmartHotel.Clients.Core.ViewModels
{
    public class HomeViewModel : ViewModelBase, IHandleViewAppearing, IHandleViewDisappearing
    {
        private bool _hasBooking;
        private Chart _temperatureChart;
        private Chart _lightChart;
        private Chart _greenChart;
        private ObservableCollection<Notification> _notifications;

        private readonly INotificationService _notificationService;
        private readonly IChartService _chartService;
        private readonly IBookingService _bookingService;
        private readonly IAuthenticationService _authenticationService;
        private readonly IRoomDevicesDataService _roomDevicesDataService;

        public HomeViewModel(
            INotificationService notificationService,
            IChartService chartService,
            IBookingService bookingService,
            IAuthenticationService authenticationService,
            IRoomDevicesDataService roomDevicesDataService)
        {
            _notificationService = notificationService;
            _chartService = chartService;
            _bookingService = bookingService;
            _authenticationService = authenticationService;
            _roomDevicesDataService = roomDevicesDataService;
            _notifications = new ObservableCollection<Notification>();
        }

        public bool HasBooking
        {
            get { return _hasBooking; }
            set
            {
                _hasBooking = value;
                OnPropertyChanged();
            }
        }

        public Microcharts.Chart TemperatureChart
        {
            get { return _temperatureChart; }

            set
            {
                _temperatureChart = value;
                OnPropertyChanged();
            }
        }

        public Microcharts.Chart LightChart
        {
            get { return _lightChart; }

            set
            {
                _lightChart = value;
                OnPropertyChanged();
            }
        }

        public Microcharts.Chart GreenChart
        {
            get { return _greenChart; }

            set
            {
                _greenChart = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<Notification> Notifications
        {
            get { return _notifications; }

            set
            {
                _notifications = value;
                OnPropertyChanged();
            }
        }

        public ICommand NotificationsCommand => new AsyncCommand(OnNotificationsAsync);

        public ICommand OpenDoorCommand => new AsyncCommand(OpenDoorAsync);

        public ICommand BookRoomCommand => new AsyncCommand(BookRoomAsync);

        public ICommand SuggestionsCommand => new AsyncCommand(SuggestionsAsync);

        public ICommand BookConferenceCommand => new AsyncCommand(BookConferenceAsync);

        public ICommand GoMyRoomCommand => new AsyncCommand(GoMyRoomAsync);

        public override async Task InitializeAsync(object navigationData)
        {
            try
            {
                IsBusy = true;

                HasBooking = AppSettings.HasBooking;

                await GetTemperatureAndLight();

                GreenChart = await _chartService.GetGreenChartAsync();

                var authenticatedUser = _authenticationService.AuthenticatedUser;
                var notifications = await _notificationService.GetNotificationsAsync(3, authenticatedUser.Token);
                Notifications = new ObservableCollection<Notification>(notifications);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Home] Error: {ex}");
                await DialogService.ShowAlertAsync(Resources.ExceptionMessage, Resources.ExceptionTitle, Resources.DialogOk);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task GetTemperatureAndLight()
        {
            var roomTemperature = await _roomDevicesDataService.GetRoomTemperatureAsync();
            var roomLight = await _roomDevicesDataService.GetRoomAmbientLightAsync();
            TemperatureChart = CreateTemperatureChart(roomTemperature);
            LightChart = CreateLightChart(roomLight);
        }

        private Chart CreateTemperatureChart(RoomTemperature roomTemperature)
        {
            var chartData = new TemperatureChart
            {
                MinValue = roomTemperature.Minimum.RawValue,
                MaxValue = roomTemperature.Maximum.RawValue
            };

            var currentChartValue = new Entry(roomTemperature.Value.RawValue) { Color = SKColor.Parse("#174A51") };
            var desiredChartValue = new Entry(roomTemperature.Desired.RawValue) { Color = SKColor.Parse("#378D93") };
            var maxChartValue = new Entry(roomTemperature.Maximum.RawValue) { Color = SKColor.Parse("#D4D4D4") };

	        chartData.CurrentValueEntry = currentChartValue;
	        chartData.DesiredValueEntry = desiredChartValue;

            if (roomTemperature.Value.RawValue > roomTemperature.Desired.RawValue)
                chartData.Entries = new[] { maxChartValue, currentChartValue, desiredChartValue  };
            else if (roomTemperature.Value.RawValue < roomTemperature.Desired.RawValue)
                chartData.Entries = new[] { maxChartValue, desiredChartValue, currentChartValue };
            else
                chartData.Entries = new[] { maxChartValue, desiredChartValue, currentChartValue  };

            return chartData;
        }

        private Chart CreateLightChart(RoomAmbientLight light)
        {
            var chartData = new LightChart()
            {
                MinValue = light.Minimum.RawValue,
                MaxValue = light.Maximum.RawValue
            };

            var lightValue = Math.Round(light.Value.RawValue);
            var currentChartValue = new Entry((float) lightValue) { Color = SKColor.Parse("#174A51") };
            var maxChartValue = new Entry(light.Maximum.RawValue) { Color = SKColor.Parse("#D4D4D4") };

	        chartData.CurrentValueEntry = currentChartValue;

            chartData.Entries = new[] { maxChartValue, currentChartValue };

            return chartData;
        }

        public Task OnViewAppearingAsync(VisualElement view)
        {
            MessagingCenter.Subscribe<Booking>(this, MessengerKeys.BookingRequested, OnBookingRequested);
            MessagingCenter.Subscribe<CheckoutViewModel>(this, MessengerKeys.CheckoutRequested, OnCheckoutRequested);

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

        private Task OnNotificationsAsync()
        {
            return NavigationService.NavigateToAsync(typeof(NotificationsViewModel), Notifications);
        }

        private Task OpenDoorAsync()
        {
            return NavigationService.NavigateToPopupAsync<OpenDoorViewModel>(true);
        }

        private Task BookRoomAsync()
        {
            return NavigationService.NavigateToAsync<BookingViewModel>();
        }

        private Task SuggestionsAsync()
        {
            return NavigationService.NavigateToAsync<SuggestionsViewModel>();
        }

        private Task BookConferenceAsync()
        {
            return NavigationService.NavigateToAsync<BookingViewModel>();
        }

        private Task GoMyRoomAsync()
        {
            if (HasBooking)
            {
                return NavigationService.NavigateToAsync<MyRoomViewModel>();
            }
            return Task.FromResult(true);
        }

        private void OnBookingRequested(Booking booking)
        {
            if (booking == null)
            {
                return;
            }

            HasBooking = true;
        }

        private void OnCheckoutRequested(object args)
        {
            HasBooking = false;
        }

	    private async void RoomDevicesDataServiceSensorDataChanged(object sender, EventArgs e)
	    {
		    await GetTemperatureAndLight();
	    }
    }
}