using System;
using System.Linq;
using System.Collections.Generic;
using System.Windows.Controls;
using OrbyLauncher.Services;
namespace OrbyLauncher.Views;
internal sealed partial class SettingsWindow
{
    private void BuildPeripheralSettings()
    {
        var section = Section("Baterias dos dispositivos", "Oculte indicadores ou corrija o tipo identificado. As escolhas ficam salvas por nome do dispositivo.");
        Check(section, "Avisar quando a bateria ficar abaixo de 15%", store.Value.BatteryLowAlerts, value => store.Update(p => p.BatteryLowAlerts = value));
        var names = PeripheralBatteries.Current.Select(d => d.Name).Concat(store.Value.HiddenBatteryDevices).Concat(store.Value.BatteryDeviceIcons.Keys).Distinct().OrderBy(n => n).ToArray();
        if (names.Length == 0) Note(section, "Os dispositivos aparecerão aqui quando houver uma leitura de bateria.");
        foreach (string name in names)
        {
            Check(section, name, !store.Value.HiddenBatteryDevices.Contains(name), value => store.Update(p =>
            { p.HiddenBatteryDevices.Remove(name); if (!value) p.HiddenBatteryDevices.Add(name); }));
            Select(section, "Ícone", store.Value.BatteryDeviceIcons.GetValueOrDefault(name, "auto"), new[]
            { new Choice("auto", "Automático"), new Choice("mouse", "Mouse"), new Choice("keyboard", "Teclado"), new Choice("headset", "Headset"), new Choice("controller", "Controle"), new Choice("phone", "Celular") },
                value => store.Update(p => { if (value == "auto") p.BatteryDeviceIcons.Remove(name); else p.BatteryDeviceIcons[name] = value; }));
        }
        section.Children.Add(Button("Atualizar lista de dispositivos", Build));
    }
}
