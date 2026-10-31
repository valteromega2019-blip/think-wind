using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ThinkWind.Services;

namespace ThinkWind.Views;

internal sealed class WeatherWidget : Grid, IDisposable
{
    private readonly SettingsStore store;
    private readonly bool configurationMode;
    private readonly DockPanel setupRow = new();
    private readonly TextBlock locationName = new() { FontSize = 16, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, MaxHeight = 44, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly TextBox city = new() { MaxLength = 100, MinHeight = 30, Padding = new Thickness(6) };
    private readonly ComboBox choices = new() { Visibility = Visibility.Collapsed, Margin = new Thickness(0, 4, 0, 0), MaxDropDownHeight = 180 };
    private readonly TextBlock temperature = new() { Text = "Escolha uma cidade", FontSize = 22, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock details = new() { FontSize = 13, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) };
    private readonly TextBlock status = new() { FontSize = 12, TextWrapping = TextWrapping.Wrap, MaxHeight = 32, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 4, 0, 0) };
    private CancellationTokenSource? request;
    private string configured = "";
    private bool active, headerActive, busy, disposed;
    internal WeatherReading? Reading { get; private set; }
    internal event Action? ReadingChanged;
    private bool Requested => active || headerActive;
    private int revision;
    private DateTimeOffset nextRefresh;
    internal bool IsInteracting => IsKeyboardFocusWithin || choices.IsDropDownOpen || IsMouseCaptureWithin;

    internal WeatherWidget(SettingsStore settings, bool configureLocation = false)
    {
        store = settings; configurationMode = configureLocation; Height = configurationMode ? 160 : 112;
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        if (!configurationMode) ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var location = new StackPanel { Margin = new Thickness(0, 0, configurationMode ? 0 : 16, 0) };
        var find = new Button { Content = "Buscar", Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(4, 0, 0, 0) };
        DockPanel.SetDock(find, Dock.Right); setupRow.Children.Add(find); setupRow.Children.Add(city);
        location.Children.Add(setupRow); location.Children.Add(choices); location.Children.Add(locationName); location.Children.Add(status);
        var source = new Button { Content = "Dados: Open-Meteo ↗", FontSize = 11, Padding = new Thickness(0), Margin = new Thickness(0, 4, 0, 0), HorizontalAlignment = HorizontalAlignment.Left };
        source.SetResourceReference(StyleProperty, "Think.IconButton"); source.Click += (_, _) => SystemActions.Run("https://open-meteo.com/");
        location.Children.Add(source); Children.Add(location);
        if (!configurationMode)
        {
            var reading = new StackPanel(); reading.Children.Add(temperature); reading.Children.Add(details); Grid.SetColumn(reading, 1); Children.Add(reading);
        }
        find.Click += async (_, _) => await Find();
        city.PreviewKeyDown += async (_, e) => { if (e.Key == System.Windows.Input.Key.Enter) { e.Handled = true; await Find(); } };
        choices.SelectionChanged += (_, _) =>
        {
            if (choices.SelectedItem is not WeatherPlace place) return;
            store.Update(p => { p.WeatherCity = place.Name; p.WeatherLatitude = place.Latitude; p.WeatherLongitude = place.Longitude; });
            choices.Visibility = Visibility.Collapsed; Configure();
        };
        IsVisibleChanged += (_, _) => { if (!IsVisible) SetActive(false); else if (configurationMode) SetActive(true); };
        Configure();
    }
    private bool HasLocation => store.Value.WeatherLatitude is double lat && double.IsFinite(lat) && lat is >= -90 and <= 90
        && store.Value.WeatherLongitude is double lon && double.IsFinite(lon) && lon is >= -180 and <= 180;
    internal void Configure()
    {
        string key = store.Value.WeatherCity + ":" + store.Value.WeatherLatitude + ":" + store.Value.WeatherLongitude;
        if (configured == key) return;
        configured = key; Cancel(); city.Text = store.Value.WeatherCity;
        Reading = null; ReadingChanged?.Invoke();
        bool editable = configurationMode || !HasLocation;
        setupRow.Visibility = editable ? Visibility.Visible : Visibility.Collapsed;
        locationName.Text = store.Value.WeatherCity;
        locationName.Visibility = HasLocation ? Visibility.Visible : Visibility.Collapsed;
        if (!editable) { choices.IsDropDownOpen = false; choices.Visibility = Visibility.Collapsed; }
        temperature.Text = HasLocation ? "Clima" : "Escolha uma cidade"; details.Text = "";
        status.Text = HasLocation ? (configurationMode ? "Cidade salva. Pesquise acima para trocar." : "Atualizando clima…") : "Digite a cidade e selecione um resultado.";
        nextRefresh = DateTimeOffset.MinValue; Poll();
    }
    internal void SetActive(bool value)
    {
        if (disposed || active == value) return;
        active = value; if (!Requested) Cancel(); else Poll();
    }
    internal void SetHeaderActive(bool value)
    {
        if (disposed || headerActive == value) return;
        headerActive = value; if (!Requested) Cancel(); else Poll();
    }
    private void Cancel() { revision++; request?.Cancel(); request?.Dispose(); request = null; if (busy) nextRefresh = DateTimeOffset.MinValue; busy = false; }
    internal void Poll()
    {
        if (configurationMode || disposed || !Requested || (!IsVisible && !headerActive) || busy || choices.Visibility == Visibility.Visible || !HasLocation || DateTimeOffset.UtcNow < nextRefresh) return;
        _ = Load();
    }
    private async Task Find()
    {
        if (!configurationMode && HasLocation) return;
        if (disposed || !active || city.Text.Trim().Length < 2) { status.Text = "Digite pelo menos duas letras da cidade."; return; }
        Cancel(); request = new CancellationTokenSource(); var token = request.Token; int current = revision; busy = true;
        choices.ItemsSource = null; choices.Visibility = Visibility.Collapsed; status.Text = "Buscando cidades…";
        try
        {
            var places = await WeatherService.FindAsync(city.Text.Trim(), token);
            if (current != revision || !active || !IsVisible) return;
            choices.ItemsSource = places; choices.Visibility = places.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
            status.Text = places.Count == 0 ? "Cidade não encontrada." : "Selecione a cidade para salvar.";
            if (places.Count > 0) choices.IsDropDownOpen = true;
        }
        catch (OperationCanceledException) { if (current == revision && active) status.Text = "Busca interrompida. Tente novamente."; }
        catch (Exception error) when (RequestError(error)) { if (current == revision && active) status.Text = "Não foi possível buscar cidades. Confira a conexão."; }
        finally { if (current == revision) busy = false; }
    }
    private async Task Load()
    {
        Cancel(); request = new CancellationTokenSource(); var token = request.Token; int current = revision; busy = true;
        nextRefresh = DateTimeOffset.UtcNow.AddMinutes(15);
        try
        {
            var weather = await WeatherService.ReadAsync(store.Value.WeatherLatitude!.Value, store.Value.WeatherLongitude!.Value, token);
            if (current != revision || !Requested || (!IsVisible && !headerActive)) return;
            Reading = weather; ReadingChanged?.Invoke();
            temperature.Text = $"{weather.Temperature:0}°C · {WeatherService.Description(weather.Code)}";
            details.Text = $"Sensação {weather.FeelsLike:0}° · mín. {weather.Minimum:0}° / máx. {weather.Maximum:0}°";
            status.Text = $"Atualizado {weather.Updated:HH:mm}";
        }
        catch (OperationCanceledException) { if (current == revision && Requested) { status.Text = "Consulta interrompida. Tentaremos novamente."; nextRefresh = DateTimeOffset.UtcNow.AddMinutes(1); } }
        catch (Exception error) when (RequestError(error)) { if (current == revision && Requested) { status.Text = "Clima indisponível. Tentaremos novamente."; nextRefresh = DateTimeOffset.UtcNow.AddMinutes(1); } }
        finally { if (current == revision) busy = false; }
    }
    private static bool RequestError(Exception error) => error is HttpRequestException or JsonException or InvalidOperationException or KeyNotFoundException or FormatException or System.IO.IOException or IndexOutOfRangeException or ArgumentException;
    public void Dispose() { disposed = true; active = false; Cancel(); }
}
